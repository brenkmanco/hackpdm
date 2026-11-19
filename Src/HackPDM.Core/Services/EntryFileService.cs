using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.Pdm;

namespace HackPDM.Core.Services;

public enum EntryOpenMode
{
	Open,
	LatestLocal,
	LatestRemote,
}

public sealed record VersionMovePlan( FileInfo From, FileInfo To, bool ToTemp );

/// <summary>Local file-system actions for the file manager: opening files, deleting local copies, and historical version downloads/moves.</summary>
public interface IEntryFileService
{
	string ResolveLocalDirectory( string? nodePath );
	Task<IReadOnlyList<string>> OpenEntriesAsync( IEnumerable<EntryRow> entries, EntryOpenMode mode );
	void OpenContainingFolders( IEnumerable<EntryRow> entries );
	bool OpenFolder( string path );
	bool OpenFile( string path );
	Task OpenLatestRemoteAsync( int entryId );
	List<FileInfo> FindLocalFiles( string directory, IEnumerable<EntryRow> entries );
	void DeleteFiles( IEnumerable<FileInfo> files );
	int? CountFiles( string directory );
	void DeleteDirectory( string directory );
	Task<HpVersion?> GetVersionAsync( int versionId );
	bool HasConflictingLocalCopy( HpVersion version );
	Task<string> DownloadVersionAsync( HpVersion version, bool toTemp );
	VersionMovePlan PlanMove( HpVersion version, bool toTemp );
	Task ExecuteMoveAsync( HpVersion version, VersionMovePlan plan );
}

public sealed class EntryFileService : IEntryFileService
{
	public string ResolveLocalDirectory( string? nodePath ) => PwaPaths.ToAbsolute( nodePath ) ?? PwaPaths.Vault;

	public async Task<IReadOnlyList<string>> OpenEntriesAsync( IEnumerable<EntryRow> entries, EntryOpenMode mode )
	{
		List<string> errors = [];
		foreach( EntryRow entry in entries )
		{
			bool hasRemoteId = entry.Id is not ( null or 0 );
			EntryOpenTarget target = mode switch
			{
				EntryOpenMode.LatestLocal => FileStatusPolicy.ForLatestLocal( hasRemoteId, entry.Status ),
				EntryOpenMode.LatestRemote => FileStatusPolicy.ForLatestRemote( hasRemoteId, entry.Status ),
				_ => FileStatusPolicy.ForOpen( hasRemoteId, entry.Status ),
			};

			switch( target )
			{
				case EntryOpenTarget.Local when entry.FullName is not null:
					OpenFile( hasRemoteId ? PwaPaths.ToAbsolute( entry.FullName ) ?? entry.FullName : entry.FullName );
					break;
				case EntryOpenTarget.Remote:
					await OpenLatestRemoteAsync( entry.Id ?? 0 );
					break;
				case EntryOpenTarget.None when mode is EntryOpenMode.LatestLocal:
					errors.Add( $"can't open remote only file locally {entry.Name}" );
					break;
				case EntryOpenTarget.None when mode is EntryOpenMode.LatestRemote:
					errors.Add( $"can't open local only file remotely {entry.Name}" );
					break;
			}
		}
		return errors;
	}

	public async Task OpenLatestRemoteAsync( int entryId )
	{
		HpVersion? version = ( await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>(
			[ entryId ], nameof( HpEntry.latest_version_id ), excludedFields: [ "preview_image" ] ) )?.FirstOrDefault();
		if( version is null )
			return;

		await version.DownloadFileAsync( Path.GetTempPath() );
		OpenFile( Path.Combine( version.WinPathway ?? "", version.name ?? "" ) );
	}

	public void OpenContainingFolders( IEnumerable<EntryRow> entries )
	{
		HashSet<string> opened = [];
		foreach( EntryRow entry in entries )
		{
			try
			{
				string? path = entry.Id is not ( null or 0 )
					? PwaPaths.ToAbsolute( entry.FullName )
					: entry.FullName;
				if( path is null )
					continue;

				FileInfo file = new( path );
				if( file.Exists && file.DirectoryName is { } dir && opened.Add( dir ) )
					ShellLauncher.OpenFolder( dir );
			}
			catch
			{
				// Ignore a bad path on one entry so the others still open.
			}
		}
	}

	public bool OpenFolder( string path ) => Directory.Exists( path ) && ShellLauncher.OpenFolder( path );
	public bool OpenFile( string path ) => ShellLauncher.OpenFile( path );

	public List<FileInfo> FindLocalFiles( string directory, IEnumerable<EntryRow> entries )
		=> [ .. entries.Select( e => new FileInfo( Path.Combine( directory, e.Name ?? "" ) ) ).Where( f => f.Exists ) ];

	public void DeleteFiles( IEnumerable<FileInfo> files )
	{
		foreach( FileInfo file in files )
			file.Delete();
	}

	public int? CountFiles( string directory )
	{
		DirectoryInfo dir = new( directory );
		return dir.Exists ? dir.EnumerateFiles().Count() : null;
	}

	public void DeleteDirectory( string directory ) => Directory.Delete( directory, true );

	public async Task<HpVersion?> GetVersionAsync( int versionId )
	{
		if( versionId is 0 )
			return null;

		HpVersion? version = await HpVersion.GetRecordByIdAsync( versionId, HpVersion.UsualExcludedFields );
		if( version is null )
			return null;

		version.WinPathway = Path.Combine( HackDefaults.Instance.PwaPathAbsolute, version.WinPathway ?? "" );
		return version;
	}

	public bool HasConflictingLocalCopy( HpVersion version )
	{
		FileInfo file = new( Path.Combine( version.WinPathway ?? "", version.name ?? "" ) );
		return file.Exists && !Checksums.Matches( file.FullName, version.checksum );
	}

	public async Task<string> DownloadVersionAsync( HpVersion version, bool toTemp )
	{
		if( !toTemp )
		{
			await version.DownloadFileAsync( version.WinPathway ?? "" );
			return version.WinPathway ?? "";
		}

		string relative = PwaPaths.ToRelative( OdooValues.Many2OneName( version.HashedValues.GetValueOrDefault( "dir_id" ) ) );
		string tempPath = Path.Combine( PdmConstants.TemporaryPath, relative );
		await version.DownloadFileAsync( tempPath );
		return tempPath;
	}

	public VersionMovePlan PlanMove( HpVersion version, bool toTemp )
	{
		FileInfo temp = new( Path.Combine( PdmConstants.TemporaryPath, version.name ?? "" ) );
		FileInfo working = new( Path.Combine( version.WinPathway ?? "", version.name ?? "" ) );
		return toTemp ? new( working, temp, true ) : new( temp, working, false );
	}

	public async Task ExecuteMoveAsync( HpVersion version, VersionMovePlan plan )
	{
		string destinationDir = plan.To.DirectoryName ?? "";
		if( plan.From.Exists )
			MoveFile( plan.From, destinationDir );
		else
			await version.DownloadFileAsync( destinationDir );
	}

	private static void MoveFile( FileInfo source, string destinationDir )
	{
		try
		{
			if( !Directory.Exists( destinationDir ) && !Directory.CreateDirectory( destinationDir ).Exists )
				return;
			string target = Path.Combine( destinationDir, source.Name );
			source.CopyTo( target, overwrite: true );
			source.Delete();
		}
		catch( Exception ex ) when( ex is IOException or UnauthorizedAccessException or ArgumentException )
		{
			HackLogger.Warn( "EntryFileService", $"Could not move '{source.FullName}' to '{destinationDir}': {ex.Message}", ex );
		}
	}
}
