using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Infrastructure.Odoo;
using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Types;
using HackPDM.Mvvm.Models.Hack;

using Path = System.IO.Path;

namespace HackPDM.Mvvm.Models.Hack;

/// <summary>Which "open" menu item the user picked.</summary>
public enum EntryOpenMode
{
	Open,
	LatestLocal,
	LatestRemote,
}

/// <summary>Source and destination of a "move version between temp and working folder" request.</summary>
public sealed record VersionMovePlan( FileInfo From, FileInfo To, bool ToTemp );

/// <summary>
/// Local file-system work for the file manager: opening files, deleting local copies,
/// and downloading or moving historical versions. No UI types.
/// </summary>
public interface IEntryFileService
{
	/// <summary>Maps a tree node path ("root\a\b") to its folder inside the local PWA.</summary>
	string ResolveLocalDirectory( string? nodePath );

	/// <summary>Opens each entry according to <paramref name="mode"/>; returns a message per entry that couldn't be opened.</summary>
	Task<IReadOnlyList<string>> OpenEntriesAsync( IEnumerable<EntryRow> entries, EntryOpenMode mode );
	void OpenContainingFolders( IEnumerable<EntryRow> entries );
	bool OpenFolder( string path );
	bool OpenFile( string path );
	/// <summary>Downloads the entry's latest version to the temp folder and opens it.</summary>
	Task OpenLatestRemoteAsync( int entryId );

	/// <summary>Local files in <paramref name="directory"/> that match the given entries.</summary>
	List<FileInfo> FindLocalFiles( string directory, IEnumerable<EntryRow> entries );
	void DeleteFiles( IEnumerable<FileInfo> files );
	/// <summary>Number of files directly inside <paramref name="directory"/>, or null if it doesn't exist.</summary>
	int? CountFiles( string directory );
	void DeleteDirectory( string directory );

	Task<HpVersion?> GetVersionAsync( int versionId );
	/// <summary>True when a different local copy of this version already exists at its working path.</summary>
	bool HasConflictingLocalCopy( HpVersion version );
	/// <summary>Downloads a version to its working folder, or to the temp folder; returns the folder used.</summary>
	string DownloadVersion( HpVersion version, bool toTemp );
	VersionMovePlan PlanMove( HpVersion version, bool toTemp );
	void ExecuteMove( HpVersion version, VersionMovePlan plan );
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
					// remote entries carry an Odoo path that has to be mapped into the local PWA
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

		version.DownloadFile( Path.GetTempPath() );
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
			catch( Exception )
			{
				// a bad path on one entry shouldn't stop the others from opening
			}
		}
	}

	public bool OpenFolder( string path )
		=> Directory.Exists( path ) && ShellLauncher.OpenFolder( path );

	public bool OpenFile( string path )
		=> ShellLauncher.OpenFile( path );

	public List<FileInfo> FindLocalFiles( string directory, IEnumerable<EntryRow> entries )
		=> [ .. entries
			.Select( e => new FileInfo( Path.Combine( directory, e.Name ?? "" ) ) )
			.Where( f => f.Exists ) ];

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

	public void DeleteDirectory( string directory )
		=> Directory.Delete( directory, true );

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

	public string DownloadVersion( HpVersion version, bool toTemp )
	{
		if( !toTemp )
		{
			version.DownloadFile( version.WinPathway ?? "" );
			return version.WinPathway ?? "";
		}

		// mirror the Odoo directory structure inside the temp folder
		string relative = PwaPaths.ToRelative( OdooValues.Many2OneName( version.HashedValues.GetValueOrDefault( "dir_id" ) ) );
		string tempPath = Path.Combine( StorageBox.TemporaryPath ?? Path.GetTempPath(), relative );
		version.DownloadFile( tempPath );
		return tempPath;
	}

	public VersionMovePlan PlanMove( HpVersion version, bool toTemp )
	{
		FileInfo temp = new( Path.Combine( StorageBox.TemporaryPath ?? "", version.name ?? "" ) );
		FileInfo working = new( Path.Combine( version.WinPathway ?? "", version.name ?? "" ) );
		return toTemp ? new( working, temp, true ) : new( temp, working, false );
	}

	public void ExecuteMove( HpVersion version, VersionMovePlan plan )
	{
		if( plan.From.Exists )
			plan.From.MoveFile( plan.To.DirectoryName ?? "" );
		else
			version.DownloadFile( plan.To.DirectoryName ?? "" );
	}
}
