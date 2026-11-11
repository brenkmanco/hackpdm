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
		Assert.True( root is not null, "hp.directory 1 not found (seed with Odoo/ci/seed_smoke_data.py)" );
		return root!;
	}

	private static async Task<SubdirectoryDto> TreeAsync()
	{
		Result<SubdirectoryDto> tree = await ( await RootAsync() ).GetSubdirectoriesAsync( false );
		Assert.True( tree.Ok, $"get_children_directories_by_id failed: {tree.Error}" );
		return tree.Value!;
	}

	private static SubdirectoryDto Child( SubdirectoryDto directory, string name )
	{
		SubdirectoryDto? child = directory.Directories.FirstOrDefault( d => d.Name == name );
		Assert.True( child is not null,
			$"'{directory.Name}' has no child '{name}'; children: {string.Join( ", ", directory.Directories.Select( d => d.Name ) )}" );
		return child!;
	}

	private static async Task<IReadOnlyList<RemoteEntryDto>> EntriesAsync( int directoryId )
	{
		var entries = await HpDirectory.GetEntriesAsync( directoryId );
		Assert.True( entries.Ok, $"get_entries({directoryId}) failed: {entries.Error}" );
		return entries.Value!;
	}

	private static async Task<int> PartsDirectoryIdAsync()
		=> Child( Child( await TreeAsync(), "Projects" ), "Parts" ).Id;

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
		var tree = await TreeAsync();

		Assert.Equal( "root", tree.Name );
		Assert.Equal( 1, tree.Id );
		Child( tree, "Library" );
		var parts = Child( Child( tree, "Projects" ), "Parts" );
		Assert.Empty( parts.Directories );
	}

	[OdooFact]
	public async Task DirectoryEntries_ListSeededFiles()
	{
		int parts = await PartsDirectoryIdAsync();

		var entries = await EntriesAsync( parts );

		Assert.Equal( SeededEntries, entries.Select( e => e.Name ).Order().ToArray() );
		foreach( RemoteEntryDto entry in entries )
		{
			Assert.True( entry.Id > 0 );
			Assert.True( entry.LatestVersionId > 0, $"{entry.Name} has no latest version" );
			Assert.Equal( "txt", entry.Type );
			Assert.EndsWith( $"Parts / {entry.Name}", entry.FullName );
		}
	}

	[OdooFact]
	public async Task DirectoryEntries_EmptyDirectory_ReturnsNoEntries()
	{
		int library = Child( await TreeAsync(), "Library" ).Id;
		Assert.Empty( await EntriesAsync( library ) );
	}

	private static async Task<int> LatestVersionIdAsync( string entryName )
	{
		var entries = await EntriesAsync( await PartsDirectoryIdAsync() );
		return entries.Single( e => e.Name == entryName ).LatestVersionId ?? 0;
	}

	[OdooFact]
	public async Task VersionFromLocal_ReturnsMetadataWithoutBlobs()
	{
		int latest = await LatestVersionIdAsync( "bracket.txt" );
		var local = new HackPDM.Core.Hack.HackFile { Name = "bracket.txt", RelativePath = @"root\Projects\Parts" };

		var (found, version) = await local.GetVersionFromLocal();

		Assert.True( found, "no version found" );
		// it used to request *only* file_contents + preview_image, so the metadata was missing
		Assert.Equal( latest, version!.id );
		Assert.Equal( "bracket.txt", version.name );
		Assert.Null( version.file_contents );
	}

	[OdooFact]
	public async Task DownloadContents_DecodesBase64FileContents()
	{
		int latest = await LatestVersionIdAsync( "bracket.txt" );
		var version = ( await HpVersion.GetRecordByIdAsync( latest, excludedFields: [ "file_contents", "preview_image" ] ) )!;

		byte[]? contents = await version.DownloadContentsAsync();

		// JSON-RPC returns base64 text; the old `as byte[]` cast always produced null
		Assert.NotNull( contents );
		Assert.Equal( "hackpdm smoke test file bracket.txt\n", System.Text.Encoding.UTF8.GetString( contents! ) );
	}

	[OdooFact]
	public async Task EntryVersions_AreReadable()
	{
		int latest = await LatestVersionIdAsync( "bracket.txt" );

		var version = await HpVersion.GetRecordByIdAsync( latest );
		Assert.True( version is not null, $"hp.version {latest} not readable" );
		Assert.Equal( "bracket.txt", version!.name );
		Assert.True( version.file_size > 0 );
	}
}
