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
/// <param name="From">Where the file is now (it may not exist, in which case the version is downloaded instead).</param>
/// <param name="To">Where the file should end up.</param>
/// <param name="ToTemp">Whether the move is from the working folder to the temp folder.</param>
public sealed record VersionMovePlan( FileInfo From, FileInfo To, bool ToTemp );

/// <summary>
/// Local file-system work for the file manager: opening files, deleting local copies,
/// and downloading or moving historical versions. No UI types.
/// </summary>
public interface IEntryFileService
{
	/// <summary>Maps a tree node path ("root\a\b") to its folder inside the local PWA.</summary>
	/// <param name="nodePath">The tree path; null maps to the vault root.</param>
	/// <returns>The absolute local folder path (which may not exist).</returns>
	string ResolveLocalDirectory( string? nodePath );

	/// <summary>Opens each entry with the system's default program, according to <paramref name="mode"/>.</summary>
	/// <param name="entries">The rows to open.</param>
	/// <param name="mode">Whether to open whichever copy fits the row's status, the local copy, or the latest server version.</param>
	/// <returns>A message per entry that couldn't be opened because the requested copy does not exist.</returns>
	/// <remarks>Opening a remote copy downloads it to the temp folder first (see <see cref="OpenLatestRemoteAsync"/>).
	/// A local file the shell fails to open is not reported.</remarks>
	Task<IReadOnlyList<string>> OpenEntriesAsync( IEnumerable<EntryRow> entries, EntryOpenMode mode );
	/// <summary>Opens the folder holding each entry's local file in the system file browser, each folder once.</summary>
	/// <param name="entries">The rows whose folders to open; rows without an existing local file are skipped.</param>
	/// <remarks>Errors for one entry (a bad path, say) are swallowed so the others still open.</remarks>
	void OpenContainingFolders( IEnumerable<EntryRow> entries );

	/// <summary>Opens a folder in the system file browser.</summary>
	/// <param name="path">The absolute folder path.</param>
	/// <returns><see langword="true"/> when the folder exists and the shell was launched.</returns>
	bool OpenFolder( string path );

	/// <summary>Opens a file with the system's default program.</summary>
	/// <param name="path">The absolute file path.</param>
	/// <returns><see langword="true"/> when the file exists and the shell was launched.</returns>
	bool OpenFile( string path );

	/// <summary>Downloads the entry's latest version to the temp folder and opens it.</summary>
	/// <param name="entryId">The Odoo entry id.</param>
	/// <remarks>Does nothing when the entry has no version. The file is opened even if the download failed.</remarks>
	Task OpenLatestRemoteAsync( int entryId );

	/// <summary>Local files in <paramref name="directory"/> that match the given entries.</summary>
	/// <param name="directory">The absolute local folder.</param>
	/// <param name="entries">The rows to look for, matched by name.</param>
	/// <returns>The files that exist, in the order of <paramref name="entries"/>.</returns>
	List<FileInfo> FindLocalFiles( string directory, IEnumerable<EntryRow> entries );

	/// <summary>Deletes local files (the server is not touched).</summary>
	/// <param name="files">The files to delete; missing files are ignored.</param>
	/// <remarks>Stops at the first file that cannot be deleted; the files before it stay deleted.</remarks>
	/// <exception cref="System.IO.IOException">A file is in use.</exception>
	/// <exception cref="System.UnauthorizedAccessException">A file is read-only (not checked out) or not accessible.</exception>
	void DeleteFiles( IEnumerable<FileInfo> files );

	/// <summary>Number of files directly inside <paramref name="directory"/>, or null if it doesn't exist.</summary>
	/// <param name="directory">The absolute local folder.</param>
	/// <returns>The file count, not including subfolders or their files; null when the folder doesn't exist.</returns>
	int? CountFiles( string directory );

	/// <summary>Deletes a local folder and everything in it (the server is not touched).</summary>
	/// <param name="directory">The absolute local folder.</param>
	/// <exception cref="System.IO.DirectoryNotFoundException">The folder does not exist.</exception>
	/// <exception cref="System.IO.IOException">A file in it is in use.</exception>
	/// <exception cref="System.UnauthorizedAccessException">A file in it is read-only or not accessible.</exception>
	void DeleteDirectory( string directory );

	/// <summary>Reads a version from the server and points its working path at the local vault.</summary>
	/// <param name="versionId">The Odoo version id; 0 gives null.</param>
	/// <returns>The version with <c>WinPathway</c> set to its absolute local folder; null when it cannot be read.</returns>
	Task<HpVersion?> GetVersionAsync( int versionId );

	/// <summary>True when a different local copy of this version already exists at its working path.</summary>
	/// <param name="version">A version from <see cref="GetVersionAsync"/>.</param>
	/// <returns><see langword="true"/> when the local file exists and its checksum differs from the version's.</returns>
	bool HasConflictingLocalCopy( HpVersion version );

	/// <summary>Downloads a version to its working folder, or to the temp folder; returns the folder used.</summary>
	/// <param name="version">A version from <see cref="GetVersionAsync"/>.</param>
	/// <param name="toTemp">Download into the temp folder (mirroring the Odoo folder structure) instead of the working folder.</param>
	/// <returns>The folder the file was written to; returned even when the download failed.</returns>
	Task<string> DownloadVersionAsync( HpVersion version, bool toTemp );

	/// <summary>Works out where a version's file moves from and to between the temp and working folders.</summary>
	/// <param name="version">A version from <see cref="GetVersionAsync"/>.</param>
	/// <param name="toTemp">Move from the working folder into the temp folder; otherwise the other way.</param>
	/// <returns>The source and destination; nothing is checked or moved yet.</returns>
	/// <remarks>The temp location is the top of the temp folder, not the mirrored structure <see cref="DownloadVersionAsync"/> uses.</remarks>
	VersionMovePlan PlanMove( HpVersion version, bool toTemp );

	/// <summary>Carries out a move plan: moves the file when the source exists, otherwise downloads the version to the destination.</summary>
	/// <param name="version">The version being moved.</param>
	/// <param name="plan">The plan from <see cref="PlanMove"/>.</param>
	/// <remarks>Failures of the move or download are not reported.</remarks>
	Task ExecuteMoveAsync( HpVersion version, VersionMovePlan plan );
}

/// <inheritdoc cref="IEntryFileService"/>
public sealed class EntryFileService : IEntryFileService
{
	/// <inheritdoc/>
	public string ResolveLocalDirectory( string? nodePath ) => PwaPaths.ToAbsolute( nodePath ) ?? PwaPaths.Vault;

	/// <inheritdoc/>
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

	/// <inheritdoc/>
	public async Task OpenLatestRemoteAsync( int entryId )
	{
		HpVersion? version = ( await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>(
			[ entryId ], nameof( HpEntry.latest_version_id ), excludedFields: [ "preview_image" ] ) )?.FirstOrDefault();
		if( version is null )
			return;

		await version.DownloadFileAsync( Path.GetTempPath() );
		OpenFile( Path.Combine( version.WinPathway ?? "", version.name ?? "" ) );
	}

	/// <inheritdoc/>
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

	/// <inheritdoc/>
	public bool OpenFolder( string path )
		=> Directory.Exists( path ) && ShellLauncher.OpenFolder( path );

	/// <inheritdoc/>
	public bool OpenFile( string path )
		=> ShellLauncher.OpenFile( path );

	/// <inheritdoc/>
	public List<FileInfo> FindLocalFiles( string directory, IEnumerable<EntryRow> entries )
		=> [ .. entries
			.Select( e => new FileInfo( Path.Combine( directory, e.Name ?? "" ) ) )
			.Where( f => f.Exists ) ];

	/// <inheritdoc/>
	public void DeleteFiles( IEnumerable<FileInfo> files )
	{
		foreach( FileInfo file in files )
			file.Delete();
	}

	/// <inheritdoc/>
	public int? CountFiles( string directory )
	{
		DirectoryInfo dir = new( directory );
		return dir.Exists ? dir.EnumerateFiles().Count() : null;
	}

	/// <inheritdoc/>
	public void DeleteDirectory( string directory )
		=> Directory.Delete( directory, true );

	/// <inheritdoc/>
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

	/// <inheritdoc/>
	public bool HasConflictingLocalCopy( HpVersion version )
	{
		FileInfo file = new( Path.Combine( version.WinPathway ?? "", version.name ?? "" ) );
		return file.Exists && !Checksums.Matches( file.FullName, version.checksum );
	}

	/// <inheritdoc/>
	public async Task<string> DownloadVersionAsync( HpVersion version, bool toTemp )
	{
		if( !toTemp )
		{
			await version.DownloadFileAsync( version.WinPathway ?? "" );
			return version.WinPathway ?? "";
		}

		// mirror the Odoo directory structure inside the temp folder
		string relative = PwaPaths.ToRelative( OdooValues.Many2OneName( version.HashedValues.GetValueOrDefault( "dir_id" ) ) );
		string tempPath = Path.Combine( StorageBox.TemporaryPath ?? Path.GetTempPath(), relative );
		await version.DownloadFileAsync( tempPath );
		return tempPath;
	}

	/// <inheritdoc/>
	public VersionMovePlan PlanMove( HpVersion version, bool toTemp )
	{
		FileInfo temp = new( Path.Combine( StorageBox.TemporaryPath ?? "", version.name ?? "" ) );
		FileInfo working = new( Path.Combine( version.WinPathway ?? "", version.name ?? "" ) );
		return toTemp ? new( working, temp, true ) : new( temp, working, false );
	}

	/// <inheritdoc/>
	public async Task ExecuteMoveAsync( HpVersion version, VersionMovePlan plan )
	{
		if( plan.From.Exists )
			plan.From.MoveFile( plan.To.DirectoryName ?? "" );
		else
			await version.DownloadFileAsync( plan.To.DirectoryName ?? "" );
	}
}
