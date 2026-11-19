using System;
using System.Linq;


using Xunit;

namespace HackPDM.Tests.Core;

public class EntryStatusResolverTests
{
	private static readonly DateTime Older = new( 2024, 1, 1 );
	private static readonly DateTime Newer = new( 2025, 1, 1 );

	[Fact]
	public void DeletedWinsOverEverything()
		=> Assert.Equal( FileStatus.Dt, EntryStatusResolver.ResolveRemote( true, 7, 7, "abc", "abc", Newer, Older ) );

	[Fact]
	public void MissingDeletedFlagIsTreatedAsDeleted()
		=> Assert.Equal( FileStatus.Dt, EntryStatusResolver.ResolveRemote( null, null, 7, "abc", "abc", Newer, Older ) );

	[Theory]
	[InlineData( 7, FileStatus.Cm )]
	[InlineData( 8, FileStatus.Co )]
	public void CheckoutBeatsChecksum( int checkoutUser, FileStatus expected )
		=> Assert.Equal( expected, EntryStatusResolver.ResolveRemote( false, checkoutUser, 7, "abc", "zzz", Newer, Older ) );

	[Fact]
	public void NoVersions() => Assert.Equal( FileStatus.NoVersions, EntryStatusResolver.ResolveRemote( false, null, 7, false, null, null, null ) );

	[Fact]
	public void RemoteOnlyWhenNoLocalFile() => Assert.Equal( FileStatus.Ro, EntryStatusResolver.ResolveRemote( false, null, 7, "abc", null, Newer, null ) );

	[Fact]
	public void MatchingChecksumIsLatest() => Assert.Equal( FileStatus.Ok, EntryStatusResolver.ResolveRemote( false, 0, 7, "abc", "abc", Newer, Older ) );

	[Fact]
	public void NewerRemoteVersion() => Assert.Equal( FileStatus.Nv, EntryStatusResolver.ResolveRemote( false, null, 7, "abc", "old", Newer, Older ) );

	[Fact]
	public void LocalModification() => Assert.Equal( FileStatus.Lm, EntryStatusResolver.ResolveRemote( false, null, 7, "abc", "edited", Older, Newer ) );

	[Fact]
	public void UnexpectedChecksumValueIsUnknown() => Assert.Equal( FileStatus.Unknown, EntryStatusResolver.ResolveRemote( false, null, 7, 42, "abc", Newer, Older ) );

	[Theory]
	[InlineData( false, false, false, false, FileStatus.Lo )]
	[InlineData( false, true, false, false, FileStatus.Ft )]
	[InlineData( true, false, false, false, FileStatus.Ft )]
	[InlineData( true, false, true, false, FileStatus.Lo )]
	[InlineData( true, false, true, true, FileStatus.If )]
	[InlineData( false, true, false, true, FileStatus.Ft )]
	public void LocalOnlyFiles( bool restrict, bool filtered, bool known, bool ignored, FileStatus expected )
		=> Assert.Equal( expected, EntryStatusResolver.ResolveLocal( restrict, filtered, known, ignored ) );
}

public class DirectoryTreeMergerTests
{
	[Fact]
	public void AddsMissingLocalFoldersUnderTheRightParent()
	{
		var root = new DirectoryNode( "root", 1 );
		var remote = new DirectoryNode( "parts", 2 );
		root.Children.Add( remote );

		DirectoryTreeMerger.AddLocalDirectories( root, [ @"root\parts", @"root\parts\new", @"root\local\deep\er" ] );

		Assert.True( remote.ExistsLocally );
		var added = Assert.Single( remote.Children );
		Assert.Equal( "new", added.Name );
		Assert.False( added.IsRemote );

		var local = root.FindChild( "local" )!;
		Assert.Equal( "er", local.FindChild( "deep" )!.FindChild( "er" )!.Name );
	}

	[Fact]
	public void AcceptsForwardSlashPaths()
	{
		var root = new DirectoryNode( "root", 1 );
		DirectoryTreeMerger.AddLocalDirectories( root, [ "root/a/b" ] );
		Assert.NotNull( root.FindChild( "a" )?.FindChild( "b" ) );
	}

	[Fact]
	public void DoesNotDuplicateExistingFolders()
	{
		var root = new DirectoryNode( "root", 1 );
		DirectoryTreeMerger.AddLocalDirectories( root, [ @"root\a", @"root\a", @"root\a\b" ] );
		Assert.Single( root.Children );
		Assert.Single( root.Children[ 0 ].Children );
	}

	[Fact]
	public void SortsChildrenCaseInsensitively()
	{
		var root = new DirectoryNode( "root", 1 );
		DirectoryTreeMerger.AddLocalDirectories( root, [ @"root\b", @"root\A", @"root\c" ] );
		root.SortRecursive();
		Assert.Equal( [ "A", "b", "c" ], root.Children.Select( c => c.Name ) );
	}

	[Fact]
	public void CountsAllFoldersRecursively()
	{
		var root = new DirectoryNode( "root", 1 );
		DirectoryTreeMerger.AddLocalDirectories( root, [ @"root\Library", @"root\Projects\Assembly", @"root\Projects\Parts" ] );
		Assert.Equal( 5, root.CountRecursive() );
	}

	[Fact]
	public void FilterPreservesAncestorsAndMatchingBranches()
	{
		var root = new DirectoryNode( "root", 1 );
		DirectoryTreeMerger.AddLocalDirectories( root, [ @"root\Library", @"root\Projects\Assembly", @"root\Projects\Parts" ] );
		root.SortRecursive();

		DirectoryNode? filtered = root.Filter( "part" );
		Assert.NotNull( filtered );
		Assert.Equal( 3, filtered!.CountRecursive() );
		Assert.Null( filtered.FindChild( "Library" ) );
		var projects = filtered.FindChild( "Projects" );
		Assert.NotNull( projects );
		Assert.Null( projects!.FindChild( "Assembly" ) );
		Assert.NotNull( projects.FindChild( "Parts" ) );
	}

	[Fact]
	public void FilterIncludesSubtreeWhenParentMatches()
	{
		var root = new DirectoryNode( "root", 1 );
		DirectoryTreeMerger.AddLocalDirectories( root, [ @"root\Library", @"root\Projects\Assembly", @"root\Projects\Parts" ] );

		DirectoryNode? filtered = root.Filter( "projects" );
		Assert.NotNull( filtered );
		var projects = filtered!.FindChild( "Projects" );
		Assert.NotNull( projects );
		Assert.Equal( 2, projects!.Children.Count );
		Assert.Null( filtered.FindChild( "Library" ) );
	}

	[Fact]
	public void FilterReturnsNullWhenNothingMatches()
	{
		var root = new DirectoryNode( "root", 1 );
		DirectoryTreeMerger.AddLocalDirectories( root, [ @"root\Library", @"root\Projects\Parts" ] );
		Assert.Null( root.Filter( "nonexistent" ) );
		Assert.Same( root, root.Filter( "   " ) );
	}
}
