using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Core.Hack;
using HackPDM.Mvvm.Models.Hack;

using Xunit;

namespace HackPDM.Tests.Core;

/// <summary>The file manager's context kept between visits of its page.</summary>
public class FileManagerStateTests
{
	private static EntryListResult Rows( int count )
		=> new( [ .. Enumerable.Range( 1, count ).Select( i => new EntryRow { Name = $"f{i}.txt" } ) ], count, 0, 0 );

	[Fact]
	public void KeptRows_AreReturnedForTheSameFolderAndFilterOnly()
	{
		FileManagerState state = new();
		EntryListResult rows = Rows( 3 );
		state.RememberEntries( @"root\a", includeDeleted: false, rows );

		Assert.True( state.TryGetEntries( @"root\a", false, out var kept ) );
		Assert.Same( rows, kept );
		Assert.True( state.TryGetEntries( @"ROOT\A", false, out _ ) ); // folder names are case-insensitive
		Assert.False( state.TryGetEntries( @"root\b", false, out _ ) );
		Assert.False( state.TryGetEntries( @"root\a", true, out _ ) ); // listed without deleted entries
	}

	[Fact]
	public void LargeEntryLists_AreNotKept()
	{
		FileManagerState state = new();
		state.RememberEntries( @"root\big", false, Rows( FileManagerState.MaxCachedEntries + 1 ) );
		Assert.False( state.TryGetEntries( @"root\big", false, out _ ) );

		state.RememberEntries( @"root\small", false, Rows( 2 ) );
		Assert.True( state.TryGetEntries( @"root\small", false, out _ ) );
	}

	[Fact]
	public void StaleEntries_AreFetchedAgain_UntilNewRowsAreKept()
	{
		FileManagerState state = new();
		state.RememberEntries( @"root\a", false, Rows( 1 ) );

		state.MarkStale( FileManagerRefresh.Entries );
		Assert.False( state.TryGetEntries( @"root\a", false, out _ ) );
		Assert.True( state.Stale.HasFlag( FileManagerRefresh.Entries ) );

		state.RememberEntries( @"root\a", false, Rows( 2 ) );
		Assert.False( state.Stale.HasFlag( FileManagerRefresh.Entries ) );
		Assert.True( state.TryGetEntries( @"root\a", false, out var kept ) );
		Assert.Equal( 2, kept.Rows.Count );
	}

	[Fact]
	public void StaleTree_IsClearedByAFreshTree()
	{
		FileManagerState state = new();
		state.MarkStale( FileManagerRefresh.Tree );
		Assert.True( state.Stale.HasFlag( FileManagerRefresh.Tree ) );

		DirectoryNode tree = new( "root", 1 );
		state.TreeRefreshed( tree );
		Assert.Same( tree, state.Tree );
		Assert.Equal( FileManagerRefresh.None, state.Stale );
	}

	[Fact]
	public async Task LocateRequests_WaitForThePage_OrGoStraightToIt()
	{
		FileManagerState state = new();
		await state.RequestLocateAsync( [ "Projects", "Parts" ], "bracket.txt" );
		Assert.Equal( "bracket.txt", state.PendingLocate?.FileName );

		var pending = state.TakePendingLocate();
		Assert.Equal( [ "Projects", "Parts" ], pending?.Segments );
		Assert.Null( state.PendingLocate );

		List<string?> handled = [];
		state.LocateRequested += ( _, file ) => { handled.Add( file ); return Task.CompletedTask; };
		await state.RequestLocateAsync( [ "Library" ], "plate.txt" );
		Assert.Equal( [ "plate.txt" ], handled );
		Assert.Null( state.PendingLocate );
	}

	[Fact]
	public void Reset_ForgetsTheContext()
	{
		FileManagerState state = new()
		{
			Tree = new DirectoryNode( "root", 1 ),
			SelectedPath = @"root\a",
			SelectedEntryNames = [ "x.txt" ],
		};
		state.ExpandedPaths.Add( "root" );
		state.RememberEntries( @"root\a", false, Rows( 1 ) );

		state.Reset();

		Assert.Null( state.Tree );
		Assert.Null( state.SelectedPath );
		Assert.Empty( state.SelectedEntryNames );
		Assert.Empty( state.ExpandedPaths );
		Assert.False( state.TryGetEntries( @"root\a", false, out _ ) );
	}
}
