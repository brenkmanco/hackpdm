using System.Collections;

using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;

namespace HackPDM.Tests.Integration;

/// <summary>
/// The server calls the file manager makes when it opens: load the root directory,
/// build the directory tree, list a directory's entries, read an entry's versions.
/// These go through the same model classes the client uses, so a change in RPC argument
/// shape (e.g. get_entries being sent [[id, flag]] instead of [id, flag]) fails here
/// without needing the UI.
/// Requires the data from Odoo/ci/seed_smoke_data.py (root / Projects / Parts with 3 entries).
/// </summary>
[Collection( OdooCollection.Name )]
public class DirectoryNavigationTests
{
	private static readonly string[] SeededEntries = [ "bracket.txt", "plate.txt", "readme.txt" ];

	public DirectoryNavigationTests() => OdooTestServer.ConfigureClient();

	private static async Task<HpDirectory> RootAsync()
	{
		await OdooTestServer.LoginAsync();
		var root = await HpDirectory.GetRecordByIdAsync( 1 );
		Assert.True( root is not null,
			$"hp.directory 1 not found (seed with Odoo/ci/seed_smoke_data.py): {OdooClient.LatestException}" );
		return root!;
	}

	private static Hashtable Child( Hashtable directory, string name )
	{
		var children = Assert.IsType<Hashtable>( directory[ "directories" ] );
		Assert.True( children.ContainsKey( name ),
			$"'{directory[ "name" ]}' has no child '{name}'; children: {string.Join( ", ", children.Keys.Cast<object>() )}" );
		return Assert.IsType<Hashtable>( children[ name ] );
	}

	private static async Task<int> PartsDirectoryIdAsync()
	{
		var tree = await ( await RootAsync() ).GetSubdirectoriesAsync( false );
		Assert.NotNull( tree );
		return Convert.ToInt32( Child( Child( tree!, "Projects" ), "Parts" )[ "id" ] );
	}

	[OdooFact]
	public async Task RootDirectory_LoadsAsRecord()
	{
		var root = await RootAsync();
		Assert.Equal( "root", root.name );
		Assert.True( root.IsRecord );
	}

	[OdooFact]
	public async Task DirectoryTree_ContainsSeededHierarchy()
	{
		var tree = await ( await RootAsync() ).GetSubdirectoriesAsync( false );
		Assert.True( tree is { Count: > 0 }, $"get_children_directories_by_id returned nothing: {OdooClient.LatestException}" );

		Assert.Equal( "root", tree![ "name" ] );
		Assert.Equal( 1, Convert.ToInt32( tree[ "id" ] ) );
		Child( tree, "Library" );
		var parts = Child( Child( tree, "Projects" ), "Parts" );
		Assert.Empty( Assert.IsType<Hashtable>( parts[ "directories" ] ) );
	}

	[OdooFact]
	public async Task DirectoryEntries_ListSeededFiles()
	{
		int parts = await PartsDirectoryIdAsync();

		Hashtable? entries = await HpDirectory.GetEntriesAsync( parts );
		Assert.True( entries is { Count: > 0 }, $"get_entries({parts}) returned nothing: {OdooClient.LatestException}" );

		Assert.Equal( SeededEntries, entries!.Keys.Cast<string>().Order().ToArray() );
		foreach( DictionaryEntry pair in entries )
		{
			var entry = Assert.IsType<Hashtable>( pair.Value );
			Assert.True( Convert.ToInt32( entry[ "id" ] ) > 0 );
			Assert.True( Convert.ToInt32( entry[ "latest" ] ) > 0, $"{pair.Key} has no latest version" );
			Assert.Equal( "txt", entry[ "type" ] );
			Assert.EndsWith( $"Parts / {pair.Key}", entry[ "fullname" ] as string );
		}
	}

	[OdooFact]
	public async Task DirectoryEntries_EmptyDirectory_ReturnsNoEntries()
	{
		var tree = await ( await RootAsync() ).GetSubdirectoriesAsync( false );
		int library = Convert.ToInt32( Child( tree!, "Library" )[ "id" ] );

		Hashtable? entries = await HpDirectory.GetEntriesAsync( library );
		Assert.True( entries is not null && string.IsNullOrEmpty( OdooClient.LatestException ),
			$"get_entries({library}) failed: {OdooClient.LatestException}" );
		Assert.Empty( entries! );
	}

	[OdooFact]
	public async Task EntryVersions_AreReadable()
	{
		int parts = await PartsDirectoryIdAsync();
		Hashtable entries = ( await HpDirectory.GetEntriesAsync( parts ) )!;
		int latest = Convert.ToInt32( ( (Hashtable)entries[ "bracket.txt" ]! )[ "latest" ] );

		var version = await HpVersion.GetRecordByIdAsync( latest );
		Assert.True( version is not null, $"hp.version {latest} not readable: {OdooClient.LatestException}" );
		Assert.Equal( "bracket.txt", version!.name );
		Assert.True( version.file_size > 0 );
	}
}
