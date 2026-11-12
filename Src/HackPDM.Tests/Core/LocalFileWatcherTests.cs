using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Mvvm.Models.Hack;

using Xunit;

namespace HackPDM.Tests.Core;

/// <summary>The working-folder watcher behind the file manager's live row updates and tree badges.</summary>
public class LocalFileWatcherTests
{
	private static readonly DateTime T0 = new( 2026, 1, 1, 0, 0, 0, DateTimeKind.Utc );

	[Theory]
	[InlineData( null, LocalChangeKind.Created, LocalChangeKind.Created )]
	[InlineData( LocalChangeKind.Created, LocalChangeKind.Updated, LocalChangeKind.Created )]
	[InlineData( LocalChangeKind.Created, LocalChangeKind.Deleted, null )]
	[InlineData( LocalChangeKind.Deleted, LocalChangeKind.Created, LocalChangeKind.Updated )] // saved by rename
	[InlineData( LocalChangeKind.Updated, LocalChangeKind.Deleted, LocalChangeKind.Deleted )]
	[InlineData( LocalChangeKind.Updated, LocalChangeKind.Updated, LocalChangeKind.Updated )]
	public void Merge_TurnsSeveralEventsIntoTheChangeTheyAddUpTo( LocalChangeKind? earlier, LocalChangeKind later, LocalChangeKind? expected )
		=> Assert.Equal( expected, LocalChangeBatcher.Merge( earlier, later ) );

	[Fact]
	public void Batcher_HandsOutAPathOnlyOnceItIsQuiet()
	{
		LocalChangeBatcher batcher = new( TimeSpan.FromMilliseconds( 400 ) );
		batcher.Add( "/v/a.txt", LocalChangeKind.Deleted, T0 );
		batcher.Add( "/v/a.txt", LocalChangeKind.Created, T0.AddMilliseconds( 100 ) );
		batcher.Add( "/v/b.txt", LocalChangeKind.Created, T0.AddMilliseconds( 100 ) );
		batcher.Add( "/v/b.txt", LocalChangeKind.Deleted, T0.AddMilliseconds( 200 ) );

		Assert.Empty( batcher.TakeDue( T0.AddMilliseconds( 450 ) ) ); // a.txt changed again at 100 ms
		var due = batcher.TakeDue( T0.AddMilliseconds( 700 ) );
		Assert.Equal( [ new LocalFileChange( "/v/a.txt", LocalChangeKind.Updated ) ], due ); // b.txt came to nothing
		Assert.Equal( 0, batcher.PendingCount );
	}

	[Theory]
	[InlineData( "~$part.SLDPRT", true )]
	[InlineData( ".~lock.notes.odt#", true )]
	[InlineData( "draft.tmp", true )]
	[InlineData( "notes.txt~", true )]
	[InlineData( "part.SLDPRT", false )]
	public void LockAndTemporaryFiles_AreIgnored( string name, bool noise )
		=> Assert.Equal( noise, LocalFileWatcher.IsNoise( Path.Combine( "vault", name ) ) );

	[Fact]
	public async Task Watcher_ReportsCreatedUpdatedAndDeletedFiles_InSubfolders()
	{
		string root = Path.Combine( Path.GetTempPath(), $"hackpdm-watch-{Guid.NewGuid():N}" );
		string sub = Directory.CreateDirectory( Path.Combine( root, "Projects", "Parts" ) ).FullName;
		ConcurrentQueue<LocalFileChange> seen = new();
		using LocalFileWatcher watcher = new();
		watcher.Changed += batch => { foreach( var change in batch ) seen.Enqueue( change ); };
		try
		{
			watcher.Watch( root );
			string file = Path.Combine( sub, "bracket.txt" );

			await File.WriteAllTextAsync( file, "v1" );
			await WaitFor( seen, file, LocalChangeKind.Created );

			await Task.Delay( 100 );
			await File.AppendAllTextAsync( file, " v2" );
			await WaitFor( seen, file, LocalChangeKind.Updated );

			File.Delete( file );
			await WaitFor( seen, file, LocalChangeKind.Deleted );

			await File.WriteAllTextAsync( Path.Combine( sub, "~$bracket.txt" ), "lock" );
			await Task.Delay( 1000 );
			Assert.DoesNotContain( seen, c => c.FullPath.EndsWith( "~$bracket.txt" ) );
		}
		finally
		{
			Directory.Delete( root, recursive: true );
		}
	}

	[Fact]
	public async Task Watcher_CreatesAMissingFolder_AndReportsFilesWrittenRightAfterTheirFolderIsMade()
	{
		string root = Path.Combine( Path.GetTempPath(), $"hackpdm-watch-{Guid.NewGuid():N}" );
		ConcurrentQueue<LocalFileChange> seen = new();
		using LocalFileWatcher watcher = new();
		watcher.Changed += batch => { foreach( var change in batch ) seen.Enqueue( change ); };
		try
		{
			watcher.Watch( root ); // nothing downloaded yet: the working folder does not exist
			Assert.True( Directory.Exists( root ) );

			// folder and parents made, file written at once: before the new folders are watched
			string sub = Directory.CreateDirectory( Path.Combine( root, "Projects", "Parts" ) ).FullName;
			string file = Path.Combine( sub, "bracket.txt" );
			await File.WriteAllTextAsync( file, "v1" );

			await WaitFor( seen, file, LocalChangeKind.Created );
		}
		finally
		{
			Directory.Delete( root, recursive: true );
		}
	}

	private static async Task WaitFor( ConcurrentQueue<LocalFileChange> seen, string path, LocalChangeKind kind )
	{
		for( int i = 0; i < 50; i++ )
		{
			if( seen.Any( c => c.FullPath == path && c.Kind == kind ) )
				return;
			await Task.Delay( 100 );
		}
		Assert.Fail( $"no {kind} for {path}; saw: {string.Join( ", ", seen.Select( c => $"{c.Kind} {Path.GetFileName( c.FullPath )}" ) )}" );
	}
}
