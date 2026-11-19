using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;

namespace HackPDM.Core.Pdm;

/// <summary>Single-version downloads, previews, local lookup, and file moves.</summary>
public static class VersionFileService
{
	private const string FileContents = "file_contents";
	private const string PreviewImage = "preview_image";

	public static async Task<(bool, HpVersion?)> GetVersionFromLocal( this HackFile hackFile )
	{
		string folder = hackFile.RelativePath is { Length: > 0 } relative
			? string.Join( PwaPaths.DirectorySeparator, [ PwaPaths.Root, .. PwaPaths.Segments( relative ) ] )
			: PwaPaths.ToServerDirectory( hackFile.DirectoryName ?? Path.GetDirectoryName( hackFile.FullPath ) ) ?? PwaPaths.Root;
		OdooDomain sameFile = OdooDomain.Where( "name", hackFile.Name ).And( "directory_complete_name", folder );
		HpVersion? version = ( await HpVersion.GetRecordsBySearchAsync( sameFile, excludedFields: [ FileContents, PreviewImage ] ) )?[ 0 ];
		return ( version is not null, version );
	}

	public static async Task<bool> GetPreviewImage( this HpVersion version )
	{
		if( version.preview_image is not { Length: > 0 } && version.id is int id and not 0 )
		{
			Result<IReadOnlyList<OdooRow>> rows = await OdooRpc.ReadAsync( HpVersion.ModelName, [ id ], [ PreviewImage ] );
			if( rows.Value is [ var row, .. ] && row.Text( PreviewImage ) is { Length: > 0 } b64 )
				version.preview_image = Convert.FromBase64String( b64 );
		}
		return version.preview_image is { Length: > 0 };
	}

	public static async Task<byte[]?> DownloadContentsAsync( this HpVersion version )
	{
		if( version.file_size == 0 || version.id is not int id || id == 0 )
			return null;
		if( ContentTransfer.IsLarge( version.file_size ) )
			return ( await ContentTransfer.DownloadBytesAsync( version ) ).Value;

		Result<IReadOnlyList<OdooRow>> rows = await OdooRpc.ReadAsync( HpVersion.ModelName, [ id ], [ FileContents ] );
		return rows.Value is [ var row, .. ] && row.Text( FileContents ) is { Length: > 0 } b64
			? Convert.FromBase64String( b64 )
			: null;
	}

	public static string LocalDirectory( this HpVersion version )
	{
		if( version.windows_complete_name is { Length: > 0 } full
			&& Path.GetDirectoryName( PwaPaths.ToAbsolute( full ) ) is { Length: > 0 } dir )
			return dir;
		return Path.Combine( HackDefaults.Instance.PwaPathAbsolute, version.WinPathway ?? "" );
	}

	public static Task<bool> DownloadFileAsync( this HpVersion version )
		=> version.DownloadFileAsync( version.LocalDirectory() );

	public static async Task<bool> DownloadFileAsync( this HpVersion version, string toPath )
	{
		if( !Directory.Exists( toPath ) && !Directory.CreateDirectory( toPath ).Exists )
			return false;
		version.file_contents ??= await version.DownloadContentsAsync();
		if( version.file_contents is { Length: > 0 } contents )
			LocalFileWriter.Write( new HackFile( version.name!, null ) { FileContents = contents, DirectoryName = toPath } );
		else if( version.file_size is not ( null or 0 ) )
			return false;
		version.WinPathway = toPath;
		return true;
	}

	public static async Task<List<HpVersion>> DownloadAllAsync( this IEnumerable<HpVersion> versions )
	{
		List<HpVersion> failed = [];
		foreach( HpVersion version in versions )
		{
			if( !await version.DownloadFileAsync() )
				failed.Add( version );
		}
		return failed;
	}

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
		catch( Exception ex ) when( ex is IOException or UnauthorizedAccessException or ArgumentException )
		{
			HackLogger.Warn( "VersionFileService", $"Could not move '{version.name}' to '{toPath}': {ex.Message}", ex );
			return false;
		}
	}

	public static HackFile ToHackFile( this HpVersion version, bool contentsInFile = false )
	{
		HackFile hack = new( version.name!, null )
		{
			Owner = version.checkout_user?.id is not null ? OdooDefaults.Instance?.OdooId == version.checkout_user.id : null,
		};
		if( version.file_contents is not { Length: > 0 } && !contentsInFile )
			return hack;
		hack.FileContents = version.file_contents;
		hack.DirectoryName = version.WinPathway;
		hack.SetModifiedDate( version.file_modify_stamp ?? default );
		hack.FileSize = version.file_size;
		return hack;
	}
}
