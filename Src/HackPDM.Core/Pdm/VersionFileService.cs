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
		if( string.IsNullOrEmpty( hackFile.Name ) )
			return ( false, null );

		string? localDir = !string.IsNullOrEmpty( hackFile.DirectoryName )
			? hackFile.DirectoryName
			: !string.IsNullOrEmpty( hackFile.FullPath )
				? Path.GetDirectoryName( hackFile.FullPath )
				: null;

		string? folder;
		if( localDir is not null )
		{
			folder = Path.IsPathFullyQualified( localDir )
				? PwaPaths.ToServerDirectory( localDir )
				: string.Join( PwaPaths.DirectorySeparator, [ PwaPaths.Root, .. PwaPaths.Segments( localDir ) ] );
			if( folder is null )
				return ( false, null );
		}
		else if( hackFile.RelativePath is { Length: > 0 } relative )
		{
			folder = string.Join( PwaPaths.DirectorySeparator, [ PwaPaths.Root, .. PwaPaths.Segments( relative ) ] );
		}
		else
		{
			folder = PwaPaths.Root;
		}

		OdooDomain sameFile = OdooDomain.Where( nameof( HpVersion.name ), hackFile.Name ).And( "directory_complete_name", folder );
		HpVersion[]? versions = await HpVersion.GetRecordsBySearchAsync( sameFile, excludedFields: [ FileContents, PreviewImage ] ).ConfigureAwait( false );
		HpVersion? version = versions?.MaxBy( static v => v.id ?? 0 );
		return ( version is not null, version );
	}

	public static async Task<byte[]?> DownloadContentsAsync( this HpVersion version, CancellationToken token = default )
	{
		if( version.id is not int id || id == 0 )
			return null;
		if( version.file_size == 0 )
			return [];
		if( ContentTransfer.IsLarge( version.file_size ) )
			return ( await ContentTransfer.DownloadBytesAsync( version, token ).ConfigureAwait( false ) ).Value;

		Result<JsonElement> raw = await OdooRpc.ExecuteAsync<JsonElement>(
			HpVersion.ModelName,
			"read",
			[ new[] { id }, new[] { FileContents } ],
			token: token ).ConfigureAwait( false );
		if( !raw.Ok || raw.Value.ValueKind != JsonValueKind.Array || raw.Value.GetArrayLength() == 0 )
			return null;
		JsonElement first = raw.Value[ 0 ];
		if( first.ValueKind != JsonValueKind.Object
			|| !first.TryGetProperty( FileContents, out JsonElement data )
			|| data.ValueKind != JsonValueKind.String )
			return null;
		if( data.TryGetBytesFromBase64( out byte[]? decoded ) )
			return decoded.Length > 0 || ( version.file_size ?? 0 ) == 0 ? decoded : null;
		string? b64 = data.GetString();
		if( b64 is null )
			return null;
		if( b64.Length == 0 )
			return ( version.file_size ?? 0 ) == 0 ? [] : null;
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

	public static Task<bool> DownloadFileAsync( this HpVersion version, CancellationToken token = default )
		=> version.DownloadFileAsync( version.LocalDirectory(), token );

	public static async Task<bool> DownloadFileAsync( this HpVersion version, string toPath, CancellationToken token = default )
	{
		if( !LocalFileWriter.IsSingleSafeFileName( version.name ) || string.IsNullOrWhiteSpace( toPath ) )
			return false;
		token.ThrowIfCancellationRequested();

		if( version.file_contents is null && ContentTransfer.IsLarge( version.file_size ) )
		{
			Result<string> temp = await ContentTransfer.DownloadToTempFileAsync( version, token ).ConfigureAwait( false );
			if( !temp.Ok || string.IsNullOrEmpty( temp.Value ) )
				return false;
			HackFile large = version.ToHackFile( contentsInFile: true );
			large.DirectoryName = toPath;
			bool moved = await LocalFileWriter.WriteFromFileInRootAsync( large, temp.Value, toPath ).ConfigureAwait( false );
			if( !moved )
			{
				FileOps.TryDelete( temp.Value );
				return false;
			}
			return true;
		}

		byte[]? contents = version.file_contents ?? ( version.file_size == 0 ? [] : await version.DownloadContentsAsync( token ).ConfigureAwait( false ) );
		if( contents is null )
			return false;
		HackFile hack = new( version.name, null )
		{
			FileContents = contents,
			DirectoryName = toPath,
			Owner = version.checkout_user?.id is not null ? OdooDefaults.Instance?.OdooId == version.checkout_user.id : null,
		};
		hack.SetModifiedDate( version.file_modify_stamp ?? default );
		bool wrote = LocalFileWriter.WriteInRoot( hack, toPath ) is not null;
		version.file_contents = null;
		return wrote;
	}

	public static async Task<List<HpVersion>> DownloadAllAsync( this IEnumerable<HpVersion> versions, CancellationToken token = default )
	{
		List<HpVersion> failed = [];
		List<(HpVersion Version, string LocalPath, string LocalChecksum)> conflictsToVerify = [];
		List<HpVersion> ready = [];

		foreach( HpVersion version in versions )
		{
			token.ThrowIfCancellationRequested();
			string localDir = version.LocalDirectory();
			string? localPath = LocalFileWriter.IsSingleSafeFileName( version.name )
				? Path.Combine( localDir, version.name )
				: null;
			if( localPath is null )
			{
				failed.Add( version );
				continue;
			}

			if( File.Exists( localPath ) )
			{
				string? expectedChecksum = DownloadOperations.ExpectedChecksum( version );
				string? localChecksum = Checksums.OfFile( localPath );
				if( expectedChecksum is { Length: > 0 } && string.Equals( localChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase ) )
					continue;
				if( localChecksum is null )
				{
					failed.Add( version );
					continue;
				}
				conflictsToVerify.Add( ( version, localPath, localChecksum ) );
			}
			else
			{
				ready.Add( version );
			}
		}

		if( conflictsToVerify.Count > 0 )
		{
			HashSet<string> known;
			try
			{
				known = await DownloadOperations.FetchVaultChecksumsForPathsAsync(
					conflictsToVerify.Select( static c => ( c.Version, c.LocalChecksum, c.Version.windows_complete_name ?? PwaPaths.ToServerName( c.LocalPath ) ) ).ToList(),
					token,
					log: null ).ConfigureAwait( false );
			}
			catch( Exception ex ) when( ex is not OperationCanceledException )
			{
				HackLogger.Warn( nameof( VersionFileService ), $"Could not verify local file modifications before version download: {ex.Message}", ex );
				foreach( var c in conflictsToVerify )
					failed.Add( c.Version );
				known = [];
				conflictsToVerify.Clear();
			}

			foreach( var (version, localPath, localChecksum) in conflictsToVerify )
			{
				string? serverPath = version.windows_complete_name ?? PwaPaths.ToServerName( localPath );
				string key = $"{serverPath}\0{localChecksum}";
				if( serverPath is null || !known.Contains( key ) )
				{
					HackLogger.Warn( nameof( VersionFileService ), $"Kept local changes to '{localPath}' instead of overwriting with historical version {version.id}." );
					failed.Add( version );
					continue;
				}
				ready.Add( version );
			}
		}

		foreach( HpVersion version in ready )
		{
			token.ThrowIfCancellationRequested();
			if( !await version.DownloadFileAsync( token ).ConfigureAwait( false ) )
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
