using System.Collections.Concurrent;
using System.Text.Json;

using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;

namespace HackPDM.Core.Pdm;

/// <summary>Single-version downloads, previews, local lookup, and file moves.</summary>
public static class VersionFileService
{
	private const string FileContents = nameof( HpVersion.file_contents );

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
		bool wrote = await LocalFileWriter.WriteInRootAsync( hack, toPath, token ).ConfigureAwait( false ) is not null;
		version.file_contents = null;
		return wrote;
	}

	public static Task<List<HpVersion>> DownloadAllAsync( this IEnumerable<HpVersion> versions, CancellationToken token = default )
	{
		HpVersion[] versionList = versions as HpVersion[] ?? [ .. versions ];
		if( versionList.Length == 0 )
			return Task.FromResult<List<HpVersion>>( [] );

		return Task.Run( async () =>
		{
			ConcurrentBag<HpVersion> failed = [];
			ConcurrentBag<(HpVersion Version, string LocalPath, string LocalChecksum)> conflictsToVerify = [];
			ConcurrentBag<HpVersion> ready = [];

			int diskParallelism = Math.Clamp( Environment.ProcessorCount, 2, 8 );
			Parallel.ForEach(
				versionList,
				new ParallelOptions { MaxDegreeOfParallelism = diskParallelism, CancellationToken = token },
				version =>
				{
					string localDir = version.LocalDirectory();
					string? localPath = LocalFileWriter.IsSingleSafeFileName( version.name )
						? Path.Combine( localDir, version.name )
						: null;
					if( localPath is null )
					{
						failed.Add( version );
						return;
					}

					if( File.Exists( localPath ) )
					{
						string? expectedChecksum = DownloadOperations.ExpectedChecksum( version );
						string? localChecksum = Checksums.OfFile( localPath );
						if( expectedChecksum is { Length: > 0 } && string.Equals( localChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase ) )
							return;
						if( localChecksum is null )
						{
							failed.Add( version );
							return;
						}
						conflictsToVerify.Add( ( version, localPath, localChecksum ) );
					}
					else
					{
						ready.Add( version );
					}
				} );

			if( !conflictsToVerify.IsEmpty )
			{
				var conflictList = conflictsToVerify.ToList();
				HashSet<string> known;
				try
				{
					known = await DownloadOperations.FetchVaultChecksumsForPathsAsync(
						conflictList.Select( static c => ( c.Version, c.LocalChecksum, c.Version.windows_complete_name ?? PwaPaths.ToServerName( c.LocalPath ) ) ).ToList(),
						token,
						log: null ).ConfigureAwait( false );
				}
				catch( Exception ex ) when( ex is not OperationCanceledException )
				{
					HackLogger.Warn( nameof( VersionFileService ), $"Could not verify local file modifications before version download: {ex.Message}", ex );
					foreach( var c in conflictList )
						failed.Add( c.Version );
					known = [];
					conflictList.Clear();
				}

				foreach( var (version, localPath, localChecksum) in conflictList )
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

			if( !ready.IsEmpty )
			{
				List<HpVersion> remoteReady = [];
				List<HpVersion> inlineReady = [];
				foreach( HpVersion version in ready )
				{
					if( version.file_contents is null && version.id is > 0 )
						remoteReady.Add( version );
					else
						inlineReady.Add( version );
				}

				int concurrency = Math.Clamp( OdooDefaults.Instance?.ConcurrencySize ?? OdooLookupCache.DefaultConcurrency, 1, 4 );
				if( remoteReady.Count > 0 )
				{
					DownloadOperations.DownloadSummary summary = new();
					await DownloadOperations.DownloadAdaptiveAsync(
						remoteReady,
						token,
						summary,
						maxBatchCount: Math.Clamp( OdooDefaults.Instance?.DownloadBatchSize ?? OdooLookupCache.DefaultBatchSize, 1, AdaptiveTransferController.MaxAdaptiveBatchCount ),
						maxConcurrency: concurrency ).ConfigureAwait( false );
					if( !summary.FailedIds.IsEmpty )
					{
						HashSet<int> failedIdSet = [ .. summary.FailedIds ];
						for( int i = 0; i < remoteReady.Count; i++ )
						{
							if( remoteReady[ i ].id is int id && failedIdSet.Contains( id ) )
								failed.Add( remoteReady[ i ] );
						}
					}
				}

				if( inlineReady.Count > 0 )
				{
					await Parallel.ForEachAsync(
						inlineReady,
						new ParallelOptions { MaxDegreeOfParallelism = concurrency, CancellationToken = token },
						async ( version, ct ) =>
						{
							if( !await version.DownloadFileAsync( ct ).ConfigureAwait( false ) )
								failed.Add( version );
						} ).ConfigureAwait( false );
				}
			}

			return failed.ToList();
		}, token );
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
