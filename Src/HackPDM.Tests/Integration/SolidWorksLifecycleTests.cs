using System.Security.Cryptography;
using System.Text;

using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.SldWrks;
using HackPDM.Mvvm.Models.Hack;
using HackPDM.Shared.GlobalData;
using HackPDM.Tests.Core;

using Xunit.Abstractions;

namespace HackPDM.Tests.Integration;

/// <summary>
/// One SolidWorks assembly with its parts taken through the whole life cycle against a live Odoo
/// server, the way a user does it in the file manager: commit a local-only assembly, lose the local
/// copies, get latest, change it, check it out, commit a new version, delete it logically, then
/// permanently. Operations go through <see cref="PdmWorkflowService"/> and statuses through
/// <see cref="EntryListService"/>, the same code the file manager uses.
/// <para>Needs the (gitignored) KitBot samples, so it is skipped where they are missing (CI).</para>
/// </summary>
[Collection( OdooCollection.Name )]
public sealed class SolidWorksLifecycleTests : IAsyncLifetime
{
	// a small sub-assembly of the KitBot: one screw and two nuts
	private const string Assembly = "am-1002 and am-1042 10-32 x 0500 and Nylock Nut Assembly_Square Configuration (19) _1__1.SLDASM";
	private static readonly string[] Parts =
	[
		"10-32x0500_shcs_HX-SHCS 0.19-32x1.375x1-C-15_1.SLDPRT",
		"10-32 Nylock Nut 1_90631A411_1.SLDPRT",
		"10-32 Nylock Nut 1_90631A411-16_1.SLDPRT",
	];
	private static string[] AllFiles => [ Assembly, .. Parts ];

	private readonly ITestOutputHelper _output;
	private readonly string _root = Path.Combine( Path.GetTempPath(), $"hackpdm-sw-{Guid.NewGuid():N}" );
	private readonly string _folder = $"SW{Guid.NewGuid():N}"[ ..14 ];
	private readonly HackPDM.Abstractions.ISettingsProvider? _previousSettings = HackDefaults.Instance.SettingsProvider;
	private readonly string _previousPwa = HackDefaults.Instance.PwaPathAbsolute;
	private readonly RecordingReporter _reporter = new();
	private readonly PdmWorkflowService _service;
	private readonly EntryListService _list = new();
	private readonly StringBuilder _steps = new();
	private int _me;

	public SolidWorksLifecycleTests( ITestOutputHelper output )
	{
		_output = output;
		_service = new PdmWorkflowService( _reporter );
	}

	private string Vault => Path.Combine( _root, "pwa" );
	private string Folder => Path.Combine( Vault, _folder );
	/// <summary>Where the original files are put when they leave the working folder.</summary>
	private string OriginalsOutside => Path.Combine( _root, "originals" );
	/// <summary>Where the downloaded copies are put (somewhere else than the originals).</summary>
	private string DownloadsOutside => Path.Combine( _root, "downloads" );
	private string TreePath => $@"root\{_folder}";

	public async Task InitializeAsync()
	{
		if( !OdooTestServer.IsConfigured || !SwSamples.HasKitBot )
			return;
		OdooTestServer.ConfigureClient();
		foreach( string dir in new[] { Folder, OriginalsOutside, DownloadsOutside } )
			Directory.CreateDirectory( dir );
		HackDefaults.Instance.SettingsProvider = new InMemorySettingsProvider();
		HackDefaults.Instance.PwaPathAbsolute = Vault;

		_me = await OdooTestServer.LoginAsync();
		OdooDefaults.Instance.OdooId = _me;
		await OdooDefaults.Instance.InitializeAsync();
		_service.PreloadMetadata();
	}

	public async Task DisposeAsync()
	{
		_output.WriteLine( _steps.ToString() );
		if( OdooTestServer.IsConfigured && SwSamples.HasKitBot )
		{
			// the parts stay on the server after the assembly is destroyed: remove them too
			List<int> leftovers = [];
			foreach( string name in AllFiles )
				if( await EntryIdAsync( name, includeDeleted: true ) is int id )
					leftovers.Add( id );
			if( leftovers.Count > 0 )
				await _service.PermanentDeleteAsync( leftovers );
		}
		HackDefaults.Instance.SettingsProvider = _previousSettings;
		HackDefaults.Instance.PwaPathAbsolute = _previousPwa;
		try { Directory.Delete( _root, true ); } catch { }
	}

	#region helpers

	private void Step( string text ) => _steps.AppendLine( $"\n== {text}" );

	private void Note( string text ) => _steps.AppendLine( $"   {text}" );

	/// <summary>Records and checks the status of <paramref name="name"/> as the file manager would show it.</summary>
	private async Task ExpectStatusAsync( string name, FileStatus expected )
	{
		FileStatus? actual = await StatusAsync( name );
		Note( $"{( actual == expected ? "ok  " : "FAIL" )} {Short( name )}: {actual?.ToString() ?? "no row"} (expected {expected})" );
		Assert.True( actual == expected, $"{name}: {actual?.ToString() ?? "no row"}, expected {expected}\n{_steps}\n{_reporter.Dump()}" );
	}

	private async Task<FileStatus?> StatusAsync( string name )
		=> ( await RowsAsync() ).FirstOrDefault( r => string.Equals( r.Name, name, StringComparison.OrdinalIgnoreCase ) )?.Status;

	/// <summary>The rows of the test folder, deleted entries included (as with "show inactive").</summary>
	private async Task<IReadOnlyList<EntryRow>> RowsAsync()
	{
		int? directoryId = await DirectoryIdAsync();
		return ( await _list.LoadAsync( directoryId, TreePath, includeDeleted: true, filterUnknownTypes: false, CancellationToken.None ) ).Rows;
	}

	private async Task<int?> DirectoryIdAsync()
	{
		var ids = ( await OdooRpc.SearchAsync( "hp.directory", OdooDomain.Where( "name", _folder ) ) ).Unwrap();
		return ids.Count > 0 ? ids[ 0 ] : null;
	}

	private async Task<int?> EntryIdAsync( string name, bool includeDeleted = false )
	{
		OdooDomain domain = OdooDomain.Where( "windows_complete_name", $@"root\{_folder}\{name}" );
		if( !includeDeleted )
			domain = domain.And( "deleted", false );
		var ids = ( await OdooRpc.SearchAsync( "hp.entry", domain ) ).Unwrap();
		return ids.Count > 0 ? ids[ 0 ] : null;
	}

	private async Task<OdooRow?> EntryAsync( string name )
	{
		var ids = ( await OdooRpc.SearchAsync( "hp.entry", OdooDomain.Where( "windows_complete_name", $@"root\{_folder}\{name}" ) ) ).Unwrap();
		if( ids.Count == 0 )
			return null;
		return ( await OdooRpc.ReadAsync( "hp.entry", ids, [ "name", "deleted", "latest_version_id", "version_ids", "checkout_user", "is_checked_out" ] ) ).Unwrap()[ 0 ];
	}

	private static async Task<OdooRow> VersionAsync( int id )
		=> ( await OdooRpc.ReadAsync( "hp.version", [ id ], [ "name", "checksum", "child_ids", "parent_ids", "entry_id" ] ) ).Unwrap()[ 0 ];

	private static int Id( object? many2one ) => OdooValues.Many2OneId( many2one ) ?? 0;

	private static IReadOnlyList<int> Ids( object? many ) => many is IEnumerable<object?> list ? [ .. list.Select( v => Convert.ToInt32( v ) ) ] : [];

	private static string Sha1( string path ) => Convert.ToHexString( SHA1.HashData( File.ReadAllBytes( path ) ) ).ToLowerInvariant();

	private static string Short( string name ) => name.Length > 40 ? name[ ..37 ] + "..." : name;

	private void MoveAll( string from, string to, IEnumerable<string> names )
	{
		foreach( string name in names )
			File.Move( Path.Combine( from, name ), Path.Combine( to, name ), overwrite: true );
	}

	private async Task<int> LastCommitIdAsync()
	{
		var ids = ( await OdooRpc.SearchAsync( "hp.pdm.commit", OdooDomain.Where( "id", ">", 0 ) ) ).Unwrap();
		return ids.Count == 0 ? 0 : ids.Max();
	}

	/// <summary>Commits the assembly row the way the file manager does: the row and the SolidWorks files it references.</summary>
	private async Task<int> CommitAssemblyAsync()
	{
		EntryRow row = ( await RowsAsync() ).Single( r => r.Name == Assembly );
		HashSet<HackFile> files = await _service.CollectCommitFilesAsync( [ row ] );
		Note( $"commit picked up {files.Count} file(s): {string.Join( ", ", files.Select( f => Short( f.Name ?? "" ) ) )}" );
		int before = await LastCommitIdAsync();
		await _service.CommitAsync( files );
		Assert.False( _reporter.Errors.Any(), _reporter.Dump() );

		var commits = ( await OdooRpc.SearchAsync( "hp.pdm.commit", OdooDomain.Where( "id", ">", before ) ) ).Unwrap();
		Assert.Single( commits );
		OdooRow commit = ( await OdooRpc.ReadAsync( "hp.pdm.commit", commits, [ "committed", "errored", "message_exception", "staged_ids" ] ) ).Unwrap()[ 0 ];
		Assert.True( commit[ "committed" ] is true, $"commit not done: {commit[ "message_exception" ]}" );
		Assert.True( commit[ "errored" ] is false );
		var staged = ( await OdooRpc.ReadAsync( "hp.record.staged", Ids( commit[ "staged_ids" ] ), [ "target_model", "target_id" ] ) ).Unwrap();
		Note( $"hp.pdm.commit {commits[ 0 ]}: committed, {staged.Count} hp.record.staged record(s): "
			+ string.Join( ", ", staged.GroupBy( s => s[ "target_model" ] ).Select( g => $"{g.Count()} {g.Key}" ) ) );
		return commits[ 0 ];
	}

	/// <summary>The checkout fields of the entry and of one of its versions, as "field=value" text ("-" for empty).</summary>
	private static async Task<(string Entry, string Version)> CheckoutFieldsAsync( int entryId, int versionId )
	{
		static string Show( OdooRow row, string[] fields ) => string.Join( ", ", fields.Select( f => $"{f}={Value( row[ f ] )}" ) );
		static string Value( object? v ) => v switch
		{
			null or false => "-",
			IEnumerable<object?> m2o when OdooValues.Many2OneId( m2o ) is int id => id.ToString(),
			_ => v.ToString() ?? "-",
		};
		string[] entryFields = [ "checkout_user", "checkout_node", "checkout_date", "is_checked_out" ];
		string[] versionFields = [ "checkout_user", "checkout_node", "checkout_date", "is_checked_out", "is_checkout_version", "node_id" ];
		OdooRow entry = ( await OdooRpc.ReadAsync( "hp.entry", [ entryId ], entryFields ) ).Unwrap()[ 0 ];
		OdooRow version = ( await OdooRpc.ReadAsync( "hp.version", [ versionId ], versionFields ) ).Unwrap()[ 0 ];
		return ( Show( entry, entryFields ), Show( version, versionFields ) );
	}

	/// <summary>Changes the file without breaking it: one more (empty) 512-byte sector at the end of the compound file.</summary>
	private static void ChangeAssembly( string path )
	{
		using FileStream stream = new( path, FileMode.Append );
		stream.Write( new byte[ 512 ] );
	}

	#endregion

	[OdooFact]
	public async Task Assembly_WithParts_GoesThroughTheWholeLifeCycle()
	{
		if( !SwSamples.HasKitBot )
			return;
		string asm = Path.Combine( Folder, Assembly );

		// ---------------------------------------------------------------- 1. local only
		Step( "1. local-only assembly and parts in the working folder" );
		foreach( string name in AllFiles )
			File.Copy( SwSamples.KitBot( name ), Path.Combine( Folder, name ) );
		var references = new SwFileReader().GetDependencies( asm ) ?? [];
		Note( $"the assembly references {references.Count} part(s): {string.Join( ", ", references.Select( r => Short( r[ 0 ] ) ) )}" );
		Assert.Equal( Parts.Order(), references.Select( r => r[ 0 ] ).Order() );
		foreach( string name in AllFiles )
		{
			Assert.Null( await EntryIdAsync( name, includeDeleted: true ) );
			await ExpectStatusAsync( name, FileStatus.Lo );
		}

		// ---------------------------------------------------------------- 2. commit
		Step( "2. commit the local-only assembly (the commit checks for an existing entry of that name in that directory)" );
		await CommitAssemblyAsync();
		Assert.Contains( _reporter.Lines, l => l.Contains( "Queued commit for " + Assembly ) );
		OdooRow entry = await EntryAsync( Assembly ) ?? throw new Xunit.Sdk.XunitException( "no entry" );
		int asmId = (int)Convert.ToInt64( entry[ "id" ] );
		int v1 = Id( entry[ "latest_version_id" ] );
		OdooRow v1Row = await VersionAsync( v1 );
		Assert.Equal( Sha1( asm ), v1Row[ "checksum" ] );
		var children = Ids( v1Row[ "child_ids" ] );
		Note( $"entry {asmId}, version {v1}, depends on {children.Count} part version(s)" );
		Note( $"version {v1} after the commit: {( await CheckoutFieldsAsync( asmId, v1 ) ).Version}" );
		Assert.Equal( Parts.Length, children.Count );
		foreach( string name in AllFiles )
			await ExpectStatusAsync( name, FileStatus.Ok );

		// ---------------------------------------------------------------- 3. local copies gone
		Step( "3. move the local files outside the working folder" );
		MoveAll( Folder, OriginalsOutside, AllFiles );
		foreach( string name in AllFiles )
			await ExpectStatusAsync( name, FileStatus.Ro );

		// ---------------------------------------------------------------- 4. get latest
		Step( "4. get latest of the assembly (downloads its parts too)" );
		_reporter.Lines.Clear();
		await _service.GetLatestAsync( [ asmId ] );
		Assert.False( _reporter.Errors.Any(), _reporter.Dump() );
		foreach( string name in AllFiles )
		{
			string downloaded = Path.Combine( Folder, name );
			Assert.True( File.Exists( downloaded ), $"{name} was not downloaded to {Folder}\n{_reporter.Dump()}" );
			Assert.Equal( Sha1( Path.Combine( OriginalsOutside, name ) ), Sha1( downloaded ) );
		}
		Note( $"all {AllFiles.Length} files downloaded to {_folder}/, identical to the originals" );
		foreach( string name in AllFiles )
			await ExpectStatusAsync( name, FileStatus.Ok );

		// ---------------------------------------------------------------- 5. local change
		Step( "5. change the downloaded assembly" );
		// get latest leaves files that are not checked out read-only (only a checkout makes them writable),
		// so changing one without checking it out means clearing that first, as a user would have to
		Assert.True( new FileInfo( asm ).IsReadOnly, "a downloaded file that is not checked out should be read-only" );
		Note( "the downloaded assembly is read-only (not checked out); cleared it to change the file anyway" );
		new FileInfo( asm ).IsReadOnly = false;
		ChangeAssembly( asm );
		Assert.Equal( Parts.Length, new SwFileReader().GetDependencies( asm )?.Count ); // still a valid assembly
		await ExpectStatusAsync( Assembly, FileStatus.Lm );
		foreach( string name in Parts )
			await ExpectStatusAsync( name, FileStatus.Ok );

		// ---------------------------------------------------------------- 6. originals back in place
		Step( "6. downloaded files out (elsewhere), originals into the working folder" );
		MoveAll( Folder, DownloadsOutside, AllFiles );
		MoveAll( OriginalsOutside, Folder, AllFiles );
		// the originals are exactly the latest version (they were committed as it), so they are up to date;
		// Nv needs a newer version on the server than the local copy: see step 10
		Note( "expected Nv in the plan; the originals ARE the latest version, so the right status is Ok" );
		foreach( string name in AllFiles )
			await ExpectStatusAsync( name, FileStatus.Ok );

		// ---------------------------------------------------------------- 7. back, minus the changed assembly
		Step( "7. originals out again; downloaded parts back, the changed assembly stays outside" );
		MoveAll( Folder, OriginalsOutside, AllFiles );
		MoveAll( DownloadsOutside, Folder, Parts );
		await ExpectStatusAsync( Assembly, FileStatus.Ro );
		foreach( string name in Parts )
			await ExpectStatusAsync( name, FileStatus.Ok );

		// ---------------------------------------------------------------- 8. checkout
		Step( "8. check out the remote-only assembly (get latest, then check out)" );
		_reporter.Lines.Clear();
		await _service.CheckoutLatestAsync( [ asmId ] );
		Assert.False( _reporter.Errors.Any(), _reporter.Dump() );
		Assert.True( File.Exists( asm ), "checkout did not get the latest first" );
		Assert.Equal( v1Row[ "checksum" ], Sha1( asm ) );
		await ExpectStatusAsync( Assembly, FileStatus.Cm );
		var (beforeCommitEntry, beforeCommitVersion) = await CheckoutFieldsAsync( asmId, v1 );
		Note( $"entry {asmId}: {beforeCommitEntry}" );
		Note( $"version {v1}: {beforeCommitVersion}" );
		Assert.Contains( $"checkout_user={_me}", beforeCommitEntry );
		Assert.Contains( $"checkout_node={OdooDefaults.Instance.MyNode!.id}", beforeCommitEntry );
		Assert.Contains( "is_checked_out=True", beforeCommitEntry );
		Assert.DoesNotContain( "checkout_date=-", beforeCommitEntry );
		Assert.Contains( $"checkout_user={_me}", beforeCommitVersion );
		Assert.Contains( "is_checkout_version=True", beforeCommitVersion );
		// the checkout is stored on the version (the entry shows it through latest_version_id); node_id is the committing node, which a checkout leaves alone
		Assert.Contains( $"node_id={OdooDefaults.Instance.MyNode!.id}", beforeCommitVersion );
		// checking out an assembly checks out what it depends on, so it can be saved
		foreach( string name in Parts )
			await ExpectStatusAsync( name, FileStatus.Cm );

		// ---------------------------------------------------------------- 9. commit the change
		Step( "9. replace the assembly with the changed one and commit it" );
		File.Delete( asm );
		File.Copy( Path.Combine( DownloadsOutside, Assembly ), asm );
		string changed = Sha1( asm );
		_reporter.Lines.Clear();
		await CommitAssemblyAsync();
		entry = await EntryAsync( Assembly ) ?? throw new Xunit.Sdk.XunitException( "entry gone" );
		int v2 = Id( entry[ "latest_version_id" ] );
		Assert.NotEqual( v1, v2 );
		Assert.Equal( changed, ( await VersionAsync( v2 ) )[ "checksum" ] );
		Assert.Equal( 2, Ids( entry[ "version_ids" ] ).Count );
		Assert.Equal( Parts.Length, Ids( ( await VersionAsync( v2 ) )[ "child_ids" ] ).Count );
		Note( $"new version {v2} with the changed checksum; checked out: {entry[ "is_checked_out" ]}" );
		var (afterEntry, afterV1) = await CheckoutFieldsAsync( asmId, v1 );
		Note( $"entry {asmId}: {afterEntry}" );
		Note( $"version {v1}: {afterV1}" );
		Note( $"version {v2}: {( await CheckoutFieldsAsync( asmId, v2 ) ).Version}" );
		Assert.Contains( "checkout_user=-", afterEntry );
		Assert.Contains( "checkout_date=-", afterEntry );
		Assert.Contains( "is_checked_out=-", afterEntry ); // false
		Assert.Contains( "checkout_user=-", afterV1 );     // the version's fields follow the entry
		await ExpectStatusAsync( Assembly, FileStatus.Ok );
		// the parts came along unchanged: no new version, but checked back in (the checkout took them too)
		Assert.Contains( _reporter.Lines, l => l.Contains( "unchanged file(s): checked in without a new version" ) );
		foreach( string name in Parts )
			await ExpectStatusAsync( name, FileStatus.Ok );

		// ---------------------------------------------------------------- 10. Nv, for real
		Step( "10. (extra) the first version in the working folder while the server has a newer one" );
		File.Move( asm, Path.Combine( DownloadsOutside, "changed-" + Assembly ) );
		File.Copy( Path.Combine( OriginalsOutside, Assembly ), asm );
		File.SetLastWriteTimeUtc( asm, DateTime.UtcNow.AddDays( -1 ) ); // saved before version 2 was committed
		await ExpectStatusAsync( Assembly, FileStatus.Nv );
		File.Delete( asm );
		File.Move( Path.Combine( DownloadsOutside, "changed-" + Assembly ), asm );
		await ExpectStatusAsync( Assembly, FileStatus.Ok );

		// ---------------------------------------------------------------- 11. logical delete
		Step( "11. logically delete the assembly" );
		_reporter.Lines.Clear();
		await _service.LogicalDeleteAsync( [ asmId ] );
		Assert.False( _reporter.Errors.Any(), _reporter.Dump() );
		entry = await EntryAsync( Assembly ) ?? throw new Xunit.Sdk.XunitException( "the entry must be kept" );
		Assert.True( entry[ "deleted" ] is true );
		Note( $"entry {asmId} kept, deleted = true, {Ids( entry[ "version_ids" ] ).Count} version(s) kept" );
		await ExpectStatusAsync( Assembly, FileStatus.Dt );
		// only the assembly: its parts may be used by other assemblies
		foreach( string name in Parts )
			await ExpectStatusAsync( name, FileStatus.Ok );

		// ---------------------------------------------------------------- 12. permanent delete
		Step( "12. permanently delete the assembly" );
		_reporter.Lines.Clear();
		Assert.True( await _service.PermanentDeleteAsync( [ asmId ] ), _reporter.Dump() );
		Assert.Null( await EntryAsync( Assembly ) );
		Assert.Empty( ( await OdooRpc.SearchAsync( "hp.version", OdooDomain.Where( "id", "in", new[] { v1, v2 } ) ) ).Unwrap() );
		Assert.True( File.Exists( asm ), "the local file must stay" );
		Note( "entry and both versions gone from the server; the local file is still there" );
		await ExpectStatusAsync( Assembly, FileStatus.Lo );
	}
}
