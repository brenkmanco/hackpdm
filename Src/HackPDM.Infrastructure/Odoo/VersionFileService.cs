using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Core;
using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo.Models;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>
/// Moving a version between the server and the working folder: finding the version of a local
/// file, downloading contents and preview, and writing or moving the local copy.
/// </summary>
public static class VersionFileService
{
	private const string FileContents = "file_contents";
	private const string PreviewImage = "preview_image";

	/// <summary>Finds the server version of a local file by its name and folder (metadata only; file contents and preview are not downloaded).</summary>
	/// <param name="hackFile">The local file. Its <c>RelativePath</c> is used for the folder when set, otherwise its directory on disk is mapped to the server's <c>root / a / b</c> form.</param>
	/// <returns>(true, version) for the first hp.version whose name and directory_complete_name match; (false, null) when none matches or the read failed.</returns>
	public static async Task<(bool, HpVersion?)> GetVersionFromLocal( this HackFile hackFile )
	{
		// the file's folder as an hp.directory complete_name ("root / a / b"): from its RelativePath
		// ("root\a\b"), else from where it is on disk
		string folder = hackFile.RelativePath is { Length: > 0 } relative
			? string.Join( PwaPaths.DirectorySeparator, [ PwaPaths.Root, .. PwaPaths.Segments( relative ) ] )
			: PwaPaths.ToServerDirectory( hackFile.DirectoryName ?? Path.GetDirectoryName( hackFile.FullPath ) ) ?? PwaPaths.Root;
		OdooDomain sameFile = OdooDomain.Where( "name", hackFile.Name ).And( "directory_complete_name", folder );
		// metadata only: the second positional parameter is includedFields, which made this
		// download just the file contents and preview image (and no metadata)
		HpVersion? version = ( await HpVersion.GetRecordsBySearchAsync( sameFile, excludedFields: [ FileContents, PreviewImage ] ) )?[ 0 ];
		return (version is not null, version);
	}

	/// <summary>Downloads the version's preview image into <c>preview_image</c> if it isn't loaded yet.</summary>
	/// <param name="version">The version; its <c>preview_image</c> is set in place.</param>
	/// <returns>True when the version has a non-empty preview image afterwards; false when it has none, its id is 0, or the read failed.</returns>
	public static async Task<bool> GetPreviewImage( this HpVersion version )
	{
		if( version.preview_image is not { Length: > 0 } && version.id is int id and not 0 )
		{
			var rows = await OdooRpc.ReadAsync( HpVersion.GetHpModel()!, [ id ], [ PreviewImage ] );
			if( rows.Value is [ var row, .. ] )
				version.preview_image = FileOperations.ConvertFromBase64( row.Text( PreviewImage ) );
		}
		return version.preview_image is { Length: > 0 };
	}

	/// <summary>Downloads the stored file contents of a version (a <c>read</c> of its <c>file_contents</c> field).</summary>
	/// <param name="version">The version to download; it is not modified.</param>
	/// <returns>The decoded bytes, or null for an empty (file_size 0) or unsaved version, or when the read failed.</returns>
	public static async Task<byte[]?> DownloadContentsAsync( this HpVersion version )
	{
		if( version.file_size == 0 || version.id is not int id || id == 0 )
			return null;
		// JSON-RPC returns binary fields as base64 strings
		var rows = await OdooRpc.ReadAsync( HpVersion.GetHpModel()!, [ id ], [ FileContents ] );
		return rows.Value is [ var row, .. ] ? FileOperations.ConvertFromBase64( row.Text( FileContents ) ) : null;
	}

	/// <summary>Works out the local folder a version belongs in, under the working folder.</summary>
	/// <param name="version">The version.</param>
	/// <returns>The folder part of <c>windows_complete_name</c> made absolute; otherwise the working folder combined with <c>WinPathway</c>.</returns>
	public static string LocalDirectory( this HpVersion version )
	{
		// windows_complete_name is "root\dir\file": the file name is part of it. WinPathway (taken
		// from it) was used as the folder, so a file landed in a folder named after itself
		if( version.windows_complete_name is { Length: > 0 } full
			&& Path.GetDirectoryName( PwaPaths.ToAbsolute( full ) ) is { Length: > 0 } dir )
			return dir;
		return Path.Combine( HackDefaults.Instance.PwaPathAbsolute, version.WinPathway ?? "" );
	}

	/// <summary>Downloads the version into its own folder (<see cref="LocalDirectory"/>).</summary>
	/// <param name="version">The version to download.</param>
	/// <returns>The result of <see cref="DownloadFileAsync(HpVersion, string)"/>.</returns>
	/// <exception cref="IOException">The folder could not be created.</exception>
	/// <exception cref="UnauthorizedAccessException">The folder could not be created due to permissions.</exception>
	public static Task<bool> DownloadFileAsync( this HpVersion version ) => version.DownloadFileAsync( version.LocalDirectory() );

	/// <summary>Downloads this version's contents (if not already loaded) and writes them into the folder <paramref name="toPath"/>.</summary>
	/// <param name="version">The version; its <c>file_contents</c> is filled and <c>WinPathway</c> set to <paramref name="toPath"/>.</param>
	/// <param name="toPath">The destination folder; created when missing.</param>
	/// <returns>False when contents could not be downloaded for a non-empty version; otherwise true (an empty file writes nothing).</returns>
	/// <remarks>The write goes through <see cref="LocalFileWriter.Write"/>, whose own failures are logged to the console rather than reported here.</remarks>
	/// <exception cref="IOException">The folder could not be created.</exception>
	/// <exception cref="UnauthorizedAccessException">The folder could not be created due to permissions.</exception>
	public static async Task<bool> DownloadFileAsync( this HpVersion version, string toPath )
	{
		if( !Directory.Exists( toPath ) && !Directory.CreateDirectory( toPath ).Exists )
			return false;
		version.file_contents ??= await version.DownloadContentsAsync();
		if( version.file_contents is { Length: > 0 } contents )
			LocalFileWriter.Write( new HackFile( version.name!, null ) { FileContents = contents, DirectoryName = toPath } );
		else if( version.file_size is not (null or 0) )
			return false;
		version.WinPathway = toPath;
		return true;
	}

	/// <summary>Downloads each version into its own folder, one after another.</summary>
	/// <param name="versions">The versions to download.</param>
	/// <returns>The versions whose download failed; empty when all succeeded.</returns>
	/// <exception cref="IOException">A folder could not be created.</exception>
	public static async Task<List<HpVersion>> DownloadAllAsync( this IEnumerable<HpVersion> versions )
	{
		List<HpVersion> failed = [];
		foreach( HpVersion version in versions )
			if( !await version.DownloadFileAsync() )
				failed.Add( version );
		return failed;
	}

	/// <summary>Moves the version's local file from <c>WinPathway</c> into the folder <paramref name="toPath"/>.</summary>
	/// <param name="version">The version; its <c>WinPathway</c> is updated on success.</param>
	/// <param name="toPath">The destination folder; created when missing.</param>
	/// <returns>True when moved; false when there is no local file or any error occurs (all exceptions are swallowed).</returns>
	public static bool MoveFile( this HpVersion version, string toPath )
	{
		try
		{
			if( !Directory.Exists( toPath ) && !Directory.CreateDirectory( toPath ).Exists )
				return false;
			FileInfo file = new( Path.Combine( version.WinPathway!, version.name! ) );
			if( !file.Exists )
				return false;
			file.MoveTo( Path.Combine( toPath, version.name! ) );
			version.WinPathway = toPath;
			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>Builds the <see cref="HackFile"/> to write for a version whose file_contents have been read.</summary>
	/// <param name="version">The version.</param>
	/// <returns>
	/// A file named after the version; <c>Owner</c> is whether the current user has it checked out (null when nobody does).
	/// Contents, folder, modified date and size are filled only when <c>file_contents</c> is non-empty.
	/// </returns>
	public static HackFile ToHackFile( this HpVersion version )
	{
		HackFile hack = new( version.name!, null )
		{
			Owner = version.checkout_user?.id is not null ? OdooDefaults.Instance?.OdooId == version.checkout_user.id : null,
		};
		if( version.file_contents is not { Length: > 0 } )
			return hack;
		hack.FileContents = version.file_contents;
		hack.DirectoryName = version.WinPathway;
		hack.SetModifiedDate( version.file_modify_stamp ?? default );
		hack.FileSize = version.file_size;
		return hack;
	}
}
