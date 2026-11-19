using System.Text.Json;

using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;

namespace HackPDM.Core.Pdm;

/// <summary>Single-version downloads, previews, local lookup, and file moves.</summary>
public static class VersionFileService
{
	private const string FileContents = nameof( HpVersion.file_contents );
	private const string PreviewImage = nameof( HpVersion.preview_image );

	public static async Task<(bool, HpVersion?)> GetVersionFromLocal( this HackFile hackFile )
	{
		string folder = hackFile.RelativePath is { Length: > 0 } relative
			? string.Join( PwaPaths.DirectorySeparator, [ PwaPaths.Root, .. PwaPaths.Segments( relative ) ] )
			: PwaPaths.ToServerDirectory( hackFile.DirectoryName ?? Path.GetDirectoryName( hackFile.FullPath ) ) ?? PwaPaths.Root;
		OdooDomain sameFile = OdooDomain.Where( nameof( HpVersion.name ), hackFile.Name ).And( "directory_complete_name", folder );
		HpVersion? version = ( await HpVersion.GetRecordsBySearchAsync( sameFile, excludedFields: [ FileContents, PreviewImage ] ) )?[ 0 ];
		return ( version is not null, version );
	}

	public static async Task<byte[]?> DownloadContentsAsync( this HpVersion version )
	{
		if( version.id is not int id || id == 0 )
			return null;
		if( version.file_size == 0 )
			return [];
		if( ContentTransfer.IsLarge( version.file_size ) )
			return ( await ContentTransfer.DownloadBytesAsync( version ).ConfigureAwait( false ) ).Value;

		Result<JsonElement> raw = await OdooRpc.ExecuteAsync<JsonElement>(
			HpVersion.ModelName,
			"read",
			[ new[] { id }, new[] { FileContents } ] ).ConfigureAwait( false );
		if( !raw.Ok || raw.Value.ValueKind != JsonValueKind.Array || raw.Value.GetArrayLength() == 0 )
			return null;
		JsonElement first = raw.Value[ 0 ];
		if( first.ValueKind != JsonValueKind.Object
			|| !first.TryGetProperty( FileContents, out JsonElement data )
			|| data.ValueKind != JsonValueKind.String )
			return null;
		if( data.TryGetBytesFromBase64( out byte[]? decoded ) )
			return decoded.Length > 0 ? decoded : null;
		string? b64 = data.GetString();
		if( string.IsNullOrEmpty( b64 ) )
			return null;
		try
		{
			return Convert.FromBase64String( b64 );
		}
		catch( FormatException ex )
		{
			HackLogger.Warn( nameof( VersionFileService ), $"Invalid base64 payload for version {id}: {ex.Message}", ex );
			return null;
		}
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
		if( string.IsNullOrEmpty( version.name ) )
			return false;
		if( !Directory.Exists( toPath ) && !Directory.CreateDirectory( toPath ).Exists )
			return false;
		byte[]? contents = version.file_contents ?? ( version.file_size == 0 ? [] : await version.DownloadContentsAsync() );
		if( contents is null )
			return false;
		HackFile hack = new( version.name, null )
		{
			FileContents = contents,
			DirectoryName = toPath,
			Owner = version.checkout_user?.id is not null ? OdooDefaults.Instance?.OdooId == version.checkout_user.id : null,
		};
		hack.SetModifiedDate( version.file_modify_stamp ?? default );
		bool wrote = LocalFileWriter.Write( hack ) is not null;
		version.file_contents = null;
		if( !wrote )
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

	public static HackFile ToHackFile( this HpVersion version, bool contentsInFile = false )
	{
		HackFile hack = new( version.name!, null )
		{
			Owner = version.checkout_user?.id is not null ? OdooDefaults.Instance?.OdooId == version.checkout_user.id : null,
		};
		if( version.file_contents is not { Length: > 0 } && !contentsInFile && ( version.file_size ?? 0 ) > 0 )
			return hack;
		hack.FileContents = version.file_contents ?? ( contentsInFile ? null : [] );
		hack.DirectoryName = version.WinPathway;
		hack.SetModifiedDate( version.file_modify_stamp ?? default );
		hack.FileSize = version.file_size;
		return hack;
	}
}
