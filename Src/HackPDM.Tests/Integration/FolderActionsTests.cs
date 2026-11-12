using System.Text;

using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Mvvm.Models.Hack;
using HackPDM.Shared.GlobalData;
using HackPDM.Tests.Core;

using Xunit.Abstractions;

namespace HackPDM.Tests.Integration;

/// <summary>
/// The directory-tree actions (commit folder, get latest with and without sub-folders, check out,
/// undo check out, delete, restore) and undo checkout of one selected assembly, on a folder holding
/// one KitBot assembly with its parts and a sub-folder holding another. Calls what the tree's menu
/// calls: <see cref="PdmWorkflowService.GetDirectoryEntryIdsAsync"/> for the folder's entries, then the
/// workflow. Statuses through <see cref="EntryListService"/>. Skipped without the KitBot samples.
/// </summary>
[Collection( OdooCollection.Name )]
public sealed class FolderActionsTests : IAsyncLifetime
{
	private const string TopAssembly = "am-1002 and am-1042 10-32 x 0500 and Nylock Nut Assembly_Square Configuration (19) _1__1.SLDASM";
	private static readonly string[] TopParts =
	[
		"10-32x0500_shcs_HX-SHCS 0.19-32x1.375x1-C-15_1.SLDPRT",
		"10-32 Nylock Nut 1_90631A411_1.SLDPRT",
		"10-32 Nylock Nut 1_90631A411-16_1.SLDPRT",
	];
	private const string SubAssembly = "am-4722 am-4722_pin am-4722_mag ToughBox S AM14U Length Hex Output Shaft_Inboard Pin  _1__1.SLDASM";
	private static readonly string[] SubParts =
	[
		"250x1000_dowel_1.SLDPRT",
		"am-4722 ToughBox S AM14U Length Hex Output Shaft_1.SLDPRT",
	];
	private static string[] TopFiles => [ TopAssembly, .. TopParts ];
	private static string[] SubFiles => [ SubAssembly, .. SubParts ];

	private readonly ITestOutputHelper _output;
	private readonly string _root = Path.Combine( Path.GetTempPath(), $"hackpdm-tree-{Guid.NewGuid():N}" );
	private readonly string _folder = $"TR{Guid.NewGuid():N}"[ ..14 ];
	private readonly HackPDM.Abstractions.ISettingsProvider? _previousSettings = HackDefaults.Instance.SettingsProvider;
	private readonly string _previousPwa = HackDefaults.Instance.PwaPathAbsolute;
	private readonly RecordingReporter _reporter = new();
	private readonly PdmWorkflowService _service;
	private readonly EntryListService _list = new();
	private readonly StringBuilder _steps = new();

	public FolderActionsTests( ITestOutputHelper output )
	{
		_output = output;
		_service = new PdmWorkflowService( _reporter );
	}

	private string Vault => Path.Combine( _root, "pwa" );
	private string Top => Path.Combine( Vault, _folder );
	private string Sub => Path.Combine( Top, "Sub" );
	private string Outside => Path.Combine( _root, "outside" );

	public async Task InitializeAsync()
	{
		if( !OdooTestServer.IsConfigured || !SwSamples.HasKitBot )
			return;
		OdooTestServer.ConfigureClient();
		foreach( string dir in new[] { Sub, Path.Combine( Outside, "Sub" ) } )
			Directory.CreateDirectory( dir );
		HackDefaults.Instance.SettingsProvider = new InMemorySettingsProvider();
		HackDefaults.Instance.PwaPathAbsolute = Vault;
		OdooDefaults.Instance.OdooId = await OdooTestServer.LoginAsync();
		await OdooDefaults.Instance.InitializeAsync();
		_service.PreloadMetadata();
	}

	public async Task DisposeAsync()
	{
		_output.WriteLine( _steps.ToString() );
		if( OdooTestServer.IsConfigured && SwSamples.HasKitBot && await DirectoryIdAsync( _folder ) is int top )
		{
			var ids = await _service.GetDirectoryEntryIdsAsync( top, withSubdirectories: true, withDeleted: true );
			if( ids.Count > 0 )
				await _service.PermanentDeleteAsync( ids );
		}
		HackDefaults.Instance.SettingsProvider = _previousSettings;
		HackDefaults.Instance.PwaPathAbsolute = _previousPwa;
		try { Directory.Delete( _root, true ); } catch { }
	}

	#region helpers

	private void Step( string text ) => _steps.AppendLine( $"\n== {text}" );
	private void Note( string text ) => _steps.AppendLine( $"   {text}" );
	private static string Short( string name ) => name.Length > 40 ? name[ ..37 ] + "..." : name;

	private static async Task<int?> DirectoryIdAsync( string name )
	{
		var ids = ( await OdooRpc.SearchAsync( "hp.directory", OdooDomain.Where( "name", name ) ) ).Unwrap();
		return ids.Count > 0 ? ids[ 0 ] : null;
	}

	/// <summary>The statuses of <paramref name="names"/> in the top folder or in Sub, as the file manager lists them.</summary>
	private async Task ExpectAsync( bool inSub, IEnumerable<string> names, FileStatus expected )
	{
		string treePath = inSub ? $@"root\{_folder}\Sub" : $@"root\{_folder}";
		int? dirId = inSub ? await SubDirectoryIdAsync() : await DirectoryIdAsync( _folder );
		var rows = ( await _list.LoadAsync( dirId, treePath, includeDeleted: true, filterUnknownTypes: false, CancellationToken.None ) ).Rows;
		foreach( string name in names )
		{
			FileStatus? actual = rows.FirstOrDefault( r => string.Equals( r.Name, name, StringComparison.OrdinalIgnoreCase ) )?.Status;
			Note( $"{( actual == expected ? "ok  " : "FAIL" )} {( inSub ? "Sub/" : "" )}{Short( name )}: {actual?.ToString() ?? "no row"} (expected {expected})" );
			Assert.True( actual == expected, $"{name}: {actual?.ToString() ?? "no row"}, expected {expected}\n{_steps}\n{_reporter.Dump()}" );
		}
	}

	private Task ExpectTopAsync( FileStatus expected ) => ExpectAsync( false, TopFiles, expected );
	private Task ExpectSubAsync( FileStatus expected ) => ExpectAsync( true, SubFiles, expected );

	private async Task<int?> SubDirectoryIdAsync()
	{
		if( await DirectoryIdAsync( _folder ) is not int top )
			return null;
		var ids = ( await OdooRpc.SearchAsync( "hp.directory", OdooDomain.Where( "name", "Sub" ).And( "parent_id", top ) ) ).Unwrap();
		return ids.Count > 0 ? ids[ 0 ] : null;
	}

	/// <summary>What the tree menu does: the selected folder's entries (and its sub-folders' when asked), then the action.</summary>
	private async Task OnFolderAsync( bool withSubdirectories, Func<IReadOnlyList<int>, Task> action, bool withDeleted = false )
	{
		int top = await DirectoryIdAsync( _folder ) ?? throw new Xunit.Sdk.XunitException( "no folder on the server" );
		var ids = await _service.GetDirectoryEntryIdsAsync( top, withSubdirectories, withDeleted );
		Note( $"folder {_folder}{( withSubdirectories ? " and its sub-folders" : " only" )}: {ids.Count} entries" );
		_reporter.Lines.Clear();
		await action( ids );
		Assert.False( _reporter.Errors.Any(), _reporter.Dump() );
	}

	private async Task<int> EntryIdAsync( string relative )
	{
		var ids = ( await OdooRpc.SearchAsync( "hp.entry", OdooDomain.Where( "windows_complete_name", $@"root\{_folder}\{relative}" ) ) ).Unwrap();
		return ids.Single();
	}

	private static void MoveAll( string from, string to, IEnumerable<string> names )
	{
		foreach( string name in names )
			File.Move( Path.Combine( from, name ), Path.Combine( to, name ), overwrite: true );
	}

	#endregion

	[OdooFact]
	public async Task TreeActions_ApplyToTheFolder_AndItsSubFolders()
	{
		if( !SwSamples.HasKitBot )
			return;

		Step( "1. commit folder (the tree's Commit), on the top folder, then on Sub" );
		foreach( string name in TopFiles )
			File.Copy( SwSamples.KitBot( name ), Path.Combine( Top, name ) );
		foreach( string name in SubFiles )
			File.Copy( SwSamples.KitBot( name ), Path.Combine( Sub, name ) );
		var topFiles = await _service.CollectFolderCommitFilesAsync( Top );
		Note( $"commit folder on the top folder picks up {topFiles.Count} file(s) (its own files only, not Sub's)" );
		Assert.Equal( TopFiles.Order(), topFiles.Select( f => f.Name! ).Order() );
		await _service.CommitAsync( topFiles );
		Assert.False( _reporter.Errors.Any(), _reporter.Dump() );
		await ExpectTopAsync( FileStatus.Ok );
		await ExpectSubAsync( FileStatus.Lo );
		await _service.CommitAsync( await _service.CollectFolderCommitFilesAsync( Sub ) );
		Assert.False( _reporter.Errors.Any(), _reporter.Dump() );
		await ExpectSubAsync( FileStatus.Ok );

		Step( "2. local files gone; get latest of the top folder only, then with sub-folders" );
		MoveAll( Top, Outside, TopFiles );
		MoveAll( Sub, Path.Combine( Outside, "Sub" ), SubFiles );
		await ExpectTopAsync( FileStatus.Ro );
		await ExpectSubAsync( FileStatus.Ro );
		await OnFolderAsync( withSubdirectories: false, _service.GetLatestAsync );
		await ExpectTopAsync( FileStatus.Ok );
		await ExpectSubAsync( FileStatus.Ro );
		await OnFolderAsync( withSubdirectories: true, _service.GetLatestAsync );
		await ExpectTopAsync( FileStatus.Ok );
		await ExpectSubAsync( FileStatus.Ok );

		Step( "3. check out the folder tree" );
		await OnFolderAsync( withSubdirectories: true, _service.CheckoutLatestAsync );
		await ExpectTopAsync( FileStatus.Cm );
		await ExpectSubAsync( FileStatus.Cm );
		Assert.False( new FileInfo( Path.Combine( Sub, SubAssembly ) ).IsReadOnly, "checked out files are writable" );

		Step( "4. undo the check out of the folder tree" );
		await OnFolderAsync( withSubdirectories: true, _service.UnCheckoutAsync );
		await ExpectTopAsync( FileStatus.Ok );
		await ExpectSubAsync( FileStatus.Ok );
		Assert.True( new FileInfo( Path.Combine( Sub, SubAssembly ) ).IsReadOnly, "released files are read-only again" );

		Step( "5. undo check out of one selected assembly (with a local change)" );
		int topAsm = await EntryIdAsync( TopAssembly );
		_reporter.Lines.Clear();
		await _service.CheckoutLatestAsync( [ topAsm ] );
		await ExpectTopAsync( FileStatus.Cm ); // the assembly and what it depends on
		await ExpectSubAsync( FileStatus.Ok );
		string asm = Path.Combine( Top, TopAssembly );
		using( FileStream stream = new( asm, FileMode.Append ) )
			stream.Write( new byte[ 512 ] );
		await _service.UnCheckoutAsync( [ topAsm ] );
		Assert.False( _reporter.Errors.Any(), _reporter.Dump() );
		Note( "undo check out releases the entry and its parts, and leaves the local change in place (not reverted)" );
		await ExpectAsync( false, [ TopAssembly ], FileStatus.Lm );
		await ExpectAsync( false, TopParts, FileStatus.Ok );
		Assert.True( new FileInfo( asm ).IsReadOnly );

		Step( "6. delete the folder tree (logically), then restore" );
		await OnFolderAsync( withSubdirectories: true, _service.LogicalDeleteAsync );
		await ExpectTopAsync( FileStatus.Dt );
		await ExpectSubAsync( FileStatus.Dt );
		int top = await DirectoryIdAsync( _folder ) ?? 0;
		await _service.RestoreDeletedInDirectoryAsync( top );
		Note( "restore works on the folder's own entries; the recursive restore is not implemented in the file manager" );
		await ExpectAsync( false, [ TopAssembly ], FileStatus.Lm );
		await ExpectAsync( false, TopParts, FileStatus.Ok );
		await ExpectSubAsync( FileStatus.Dt );
		await _service.RestoreDeletedInDirectoryAsync( await SubDirectoryIdAsync() ?? 0 );
		await ExpectSubAsync( FileStatus.Ok );
	}
}
