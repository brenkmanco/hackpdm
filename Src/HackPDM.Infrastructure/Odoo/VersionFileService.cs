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

	/// <summary>The server version of a local file (metadata only), by name and folder.</summary>
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

	/// <summary>Reads the preview image when it hasn't been; true when there is one.</summary>
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

	/// <summary>The stored contents of a version (null for an empty or unsaved one).</summary>
	public static byte[]? DownloadContents( this HpVersion version )
	{
		if( version.file_size == 0 || version.id is not int id || id == 0 )
			return null;
		// JSON-RPC returns binary fields as base64 strings
		var rows = OdooRpc.ReadAsync( HpVersion.GetHpModel()!, [ id ], [ FileContents ] ).GetAwaiter().GetResult();
		return rows.Value is [ var row, .. ] ? FileOperations.ConvertFromBase64( row.Text( FileContents ) ) : null;
	}

	/// <summary>The local folder this version belongs in, under the working folder.</summary>
	public static string LocalDirectory( this HpVersion version )
	{
		// windows_complete_name is "root\dir\file": the file name is part of it. WinPathway (taken
		// from it) was used as the folder, so a file landed in a folder named after itself
		if( version.windows_complete_name is { Length: > 0 } full
			&& Path.GetDirectoryName( PwaPaths.ToAbsolute( full ) ) is { Length: > 0 } dir )
			return dir;
		return Path.Combine( HackDefaults.Instance.PwaPathAbsolute, version.WinPathway ?? "" );
	}

	public static bool DownloadFile( this HpVersion version ) => version.DownloadFile( version.LocalDirectory() );

	/// <summary>Writes this version's contents into <paramref name="toPath"/>; false when nothing could be downloaded.</summary>
	public static bool DownloadFile( this HpVersion version, string toPath )
	{
		if( !Directory.Exists( toPath ) && !Directory.CreateDirectory( toPath ).Exists )
			return false;
		version.file_contents ??= version.DownloadContents();
		if( version.file_contents is { Length: > 0 } contents )
			LocalFileWriter.Write( new HackFile( version.name!, null ) { FileContents = contents, DirectoryName = toPath } );
		else if( version.file_size is not (null or 0) )
			return false;
		version.WinPathway = toPath;
		return true;
	}

	/// <summary>Downloads each version into its folder; the ones that failed come back in <paramref name="failedDownloads"/>.</summary>
	public static bool DownloadAll( this IEnumerable<HpVersion> versions, out List<HpVersion> failedDownloads )
	{
		failedDownloads = [ .. versions.Where( version => !version.DownloadFile() ) ];
		return failedDownloads.Count == 0;
	}

	/// <summary>Moves the local copy into <paramref name="toPath"/>; false when there is none or it can't be moved.</summary>
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

	/// <summary>The file to write for a version whose file_contents have been read.</summary>
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
