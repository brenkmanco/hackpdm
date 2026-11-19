using System.Text.RegularExpressions;

using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.SolidWorks;

namespace HackPDM.Core.Pdm;

/// <summary>Filters commit candidates, ensures server directories exist, and stages records for commit.</summary>
public static class CommitOperations
{
	public static async Task<HpDirectory?> CreateDirectoriesAsync( HackFile? hack )
	{
		if( hack is null )
			return null;
		return ( await HpDirectory.CreateNew( DirectorySegments( hack ) ) )?.LastOrDefault()
			?? throw new InvalidOperationException( $"{HpDirectory.ModelName} didn't create any records" );
	}

	internal static string[] DirectorySegments( HackFile hack )
		=> PwaPaths.LocalSegments( hack.FullPath is { Length: > 0 } full ? Path.GetDirectoryName( full ) : hack.DirectoryName ) ?? [];

	internal static string ServerWindowsName( HackFile hack )
		=> PwaPaths.ToServerName( [ .. DirectorySegments( hack ), hack.Name ?? "" ] );

	public static async Task<(HackFile[] LocalOnly, HackFile[] RemoteExisting, HpEntry[] UnchangedCheckedOut)> FilesNotInOdooSegmentedAsync(
		IEnumerable<HackFile> hackFiles,
		Action<StatusMessage, string>? log = null )
	{
		List<HackFile> localOnly = [];
		List<HackFile> remoteExisting = [];
		List<HpEntry> unchanged = [];

		foreach( HackFile hack in hackFiles )
		{
			string? ext = hack.TypeExt?.ToLower();
			if( OdooDefaults.Instance.RestrictTypes is true && !OdooDefaults.Instance.ExtToType.ContainsKey( ext ?? "-=-=-" ) )
			{
				log?.Invoke( StatusMessage.SKIP, $"not a file type the vault accepts, not committed: {hack.Name}" );
				continue;
			}

			string filePath = PwaPaths.ToServerDirectory( hack.DirectoryName ) ?? hack.DirectoryName ?? "";
			OdooDomain sameFile = OdooDomain.Where( "name", hack.Name )
				.And( "windows_complete_name", ServerWindowsName( hack ) )
				.And( "deleted", false );

			HpEntry? entry = ( await HpEntry.GetRecordsBySearchAsync(
				searchFilter: sameFile,
				includedFields: [ nameof( HpEntry.id ), nameof( HpEntry.name ), nameof( HpEntry.dir_id ), nameof( HpEntry.checkout_user ), nameof( HpEntry.latest_version_id ), nameof( HpEntry.windows_complete_name ) ] ) )?.FirstOrDefault();

			if( entry is { id: not null and not 0 } )
			{
				bool mine = entry.checkout_user?.id == OdooDefaults.Instance.OdooId;
				if( await LatestChecksumAsync( entry ) is { Length: > 0 } latest
					&& string.Equals( latest, hack.GetChecksum(), StringComparison.OrdinalIgnoreCase ) )
				{
					log?.Invoke( StatusMessage.SKIP, mine
						? $"unchanged since the latest version, checked in without a new version: {filePath}"
						: $"unchanged since the latest version, nothing to commit: {filePath}" );
					if( mine )
						unchanged.Add( entry );
					continue;
				}
				if( mine )
				{
					log?.Invoke( StatusMessage.FOUND, $"entry found remotely for: {filePath}" );
					remoteExisting.Add( hack );
					continue;
				}
				log?.Invoke( StatusMessage.ERROR, $"entry found remotely but is not checked out to current user: {filePath}" );
				continue;
			}

			log?.Invoke( StatusMessage.INFO, $"Queued commit for {hack.Name} (Checksum: {hack.Checksum}) for: {filePath}" );
			localOnly.Add( hack );
		}
		return ( [ .. localOnly ], [ .. remoteExisting ], [ .. unchanged ] );
	}

	public static async Task<bool> CommitRecordAsync(
		HackFile? hack,
		HpPDMCommit commit,
		bool inOdoo = false,
		Action<StatusMessage, string>? log = null,
		Action? onRecordStaged = null,
		Action<long>? onBytesUploaded = null )
	{
		if( hack is null )
			return false;
		if( hack.FullPath is null || !File.Exists( hack.FullPath ) )
		{
			log?.Invoke( StatusMessage.ERROR, $"{hack.Name}: the file is no longer there ({hack.FullPath}); nothing was committed" );
			await commit.ServerClear();
			return false;
		}

		HpDirectory? dir = await CreateDirectoriesAsync( hack );
		HpEntry entry = CommitStaging.NewEntry( hack, dir?.id ?? 0, commit ) ?? new();
		if( inOdoo )
			entry = await CommitStaging.FindRemoteEntryAsync( entry ) ?? entry;

		HpRecordStaged? versionStaged = null;
		string? stageError = null;
		if( entry.returnType is EntryReturnType.GotExisting )
		{
			log?.Invoke( StatusMessage.FOUND, "existing entry found..." );
			log?.Invoke( StatusMessage.PROCESSING, "staging local version..." );
			versionStaged = await CommitStaging.StageVersionAsync( hack, entry, commit.id ?? 0, e => stageError = e, onBytesUploaded );
		}
		if( entry.returnType is EntryReturnType.Staged )
		{
			HpRecordStaged? entryStaged = await CommitStaging.StageEntryAsync( entry );
			log?.Invoke( StatusMessage.PROCESSING, "staging local entry..." );
			log?.Invoke( StatusMessage.PROCESSING, "staging local version..." );
			versionStaged = entryStaged is null ? null : await CommitStaging.StageVersionAsync( hack, entryStaged, e => stageError = e, onBytesUploaded );
		}
		if( entry.returnType is EntryReturnType.Failed )
		{
			await commit.ServerClear();
			log?.Invoke( StatusMessage.ERROR, $"Error creating entry {entry.name}. Rolling back records..." );
			return false;
		}

		try
		{
			if( versionStaged is not { id: not 0 } )
				throw new ArgumentException( $"Could not stage {hack.Name}: {stageError ?? "the version was not created"}" );

			if( !CanReadReferences( hack, log ) )
			{
				onRecordStaged?.Invoke();
				return true;
			}

			log?.Invoke( StatusMessage.PROCESSING, "staging local version relationships..." );
			if( !await CommitStaging.StageRelationshipsAsync( versionStaged ) )
				throw new ArgumentException( $"Failed to stage version relationships for {versionStaged.payload?.GetValueOrDefault( "name" )}" );

			log?.Invoke( StatusMessage.PROCESSING, "staging local version properties ..." );
			if( !await CommitStaging.StagePropertiesAsync( versionStaged ) )
				throw new ArgumentException( $"Failed to stage version properties for {versionStaged.payload?.GetValueOrDefault( "name" )}" );

			onRecordStaged?.Invoke();
		}
		catch( Exception e )
		{
			await commit.ServerClear();
			HackLogger.Error( "PdmCommit", $"Staging failed for commit {commit.id}: {e.Message}", e );
			log?.Invoke( StatusMessage.ERROR, e.Message );
			return false;
		}
		return true;
	}

	private static bool CanReadReferences( HackFile hack, Action<StatusMessage, string>? log )
	{
		if( !SwDocumentTypes.IsSolidWorksExtension( hack.TypeExt ) || hack.FullPath is null )
			return true;
		try
		{
			SolidWorksUtil.DocMgr?.GetDependencies( hack.FullPath );
			return true;
		}
		catch( Exception e )
		{
			log?.Invoke( StatusMessage.WARNING, $"{hack.Name}: references and properties can't be read ({e.Message}); committing the file without them" );
			return false;
		}
	}

	private static async Task<string?> LatestChecksumAsync( HpEntry entry )
	{
		if( entry.latest_version_id?.id is not int versionId || versionId == 0 )
			return null;
		return ( await HpVersion.GetRecordsByIdsAsync( [ versionId ], includedFields: [ nameof( HpVersion.checksum ) ] ) )?.FirstOrDefault()?.checksum;
	}

	private static Regex? EntryFilter()
	{
		string[] patterns = [ .. ( OdooDefaults.Instance?.EntryFilterPatterns ?? [] ).Where( p => !string.IsNullOrEmpty( p ) ) ];
		return patterns.Length == 0 ? null : new Regex( string.Join( "|", patterns ), RegexOptions.IgnoreCase );
	}

	public static bool IsFilteredName( string? name )
		=> EntryFilter() is { } regex && regex.IsMatch( name ?? "" );

	public static Task<(HackFile[] LocalOnly, HackFile[] RemoteExisting, HpEntry[] UnchangedCheckedOut)> FilterCommitHackFilesAsync(
		IEnumerable<HackFile> hackFiles,
		Action<StatusMessage, string>? log = null )
	{
		Regex? regex = EntryFilter();
		List<HackFile> kept = [ .. hackFiles.Where( h => regex is null || !regex.IsMatch( h.Name ?? "" ) ) ];
		return FilesNotInOdooSegmentedAsync( kept, log );
	}
}
