using System.Security.Cryptography;

using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Infrastructure.Operations;
using HackPDM.Mvvm.Models.Hack;
using HackPDM.Shared.GlobalData;

using Xunit.Abstractions;

namespace HackPDM.Tests.Integration;

/// <summary>
/// Tries to break the vault through the client's own code, as two regular (non-administrator)
/// users, Alice and Bob, each with a working folder of their own: taking or committing a file the
/// other one holds, committing around the client's checks, losing local work to Get Latest, and
/// file names and situations the server must refuse cleanly. The administrator the other live tests
/// use bypasses the server's user-level guards, so it would hide most of these.
/// </summary>
[Collection( OdooCollection.Name )]
public sealed class AttackTests : IAsyncLifetime
{
	private const string Password = "hackpdm-attack-pw";

	private readonly ITestOutputHelper _output;
	private readonly string _root = Path.Combine( Path.GetTempPath(), $"hackpdm-atk-{Guid.NewGuid():N}" );
	private readonly string _folder = $"AT{Guid.NewGuid():N}"[ ..14 ];
	private readonly HackPDM.Abstractions.ISettingsProvider? _previousSettings = HackDefaults.Instance.SettingsProvider;
	private readonly string _previousPwa = HackDefaults.Instance.PwaPathAbsolute;
	private readonly RecordingReporter _reporter = new();
	private readonly PdmWorkflowService _service;
	private string _alice = "", _bob = "";
	private int _aliceId, _bobId;

	public AttackTests( ITestOutputHelper output )
	{
		_output = output;
		_service = new PdmWorkflowService( _reporter );
	}

	public async Task InitializeAsync()
	{
		if( !OdooTestServer.IsConfigured )
			return;
		OdooTestServer.ConfigureClient();
		HackDefaults.Instance.SettingsProvider = new InMemorySettingsProvider();
		OdooDefaults.Instance.OdooId = await OdooTestServer.LoginAsync();
		await OdooDefaults.Instance.InitializeAsync();
		_service.PreloadMetadata();

		string tag = Guid.NewGuid().ToString( "N" )[ ..8 ];
		(_alice, _aliceId) = await CreateUserAsync( $"atk-alice-{tag}" );
		(_bob, _bobId) = await CreateUserAsync( $"atk-bob-{tag}" );
		await AsAliceAsync();
	}

	public async Task DisposeAsync()
	{
		_output.WriteLine( _reporter.Dump() );
		if( OdooTestServer.IsConfigured )
		{
			await AsAdminAsync();
			if( await DirectoryIdAsync() is int dir )
			{
				var ids = await _service.GetDirectoryEntryIdsAsync( dir, withSubdirectories: true, withDeleted: true );
				if( ids.Count > 0 )
					await _service.PermanentDeleteAsync( ids );
			}
		}
		HackDefaults.Instance.SettingsProvider = _previousSettings;
		HackDefaults.Instance.PwaPathAbsolute = _previousPwa;
		try { Directory.Delete( _root, true ); } catch { }
	}

	#region users and helpers

	private static async Task<(string Login, int Id)> CreateUserAsync( string login )
	{
		int id = ( await OdooRpc.CreateAsync( "res.users", new Dictionary<string, object?>
		{
			[ "name" ] = login, [ "login" ] = login, [ "password" ] = Password,
		} ) ).Unwrap();
		Assert.True( id > 0 );
		return (login, id);
	}

	private async Task LoginAsync( string login, string password, string vault )
	{
		OdooDefaults.Instance.OdooUser = login;
		OdooDefaults.Instance.OdooPass = password;
		OdooDefaults.Instance.OdooId = await OdooTestServer.LoginAsync();
		Directory.CreateDirectory( vault );
		HackDefaults.Instance.PwaPathAbsolute = vault;
	}

	private string AliceVault => Path.Combine( _root, "alice" );
	private string BobVault => Path.Combine( _root, "bob" );

	private Task AsAliceAsync() => LoginAsync( _alice, Password, AliceVault );
	private Task AsBobAsync() => LoginAsync( _bob, Password, BobVault );
	private Task AsAdminAsync() => LoginAsync( OdooTestServer.User, OdooTestServer.Password, AliceVault );

	private string PathIn( string vault, string name ) => Path.Combine( vault, _folder, name );

	private string Write( string vault, string name, string contents )
	{
		string path = PathIn( vault, name );
		Directory.CreateDirectory( Path.GetDirectoryName( path )! );
		if( File.Exists( path ) )
			File.SetAttributes( path, FileAttributes.Normal ); // downloaded copies are read-only
		File.WriteAllText( path, contents );
		return path;
	}

	private static HackFile Hack( string path ) => HackFile.GetFromPath( path ) ?? throw new InvalidOperationException( path );

	private async Task<int?> DirectoryIdAsync()
	{
		var ids = ( await OdooRpc.SearchAsync( "hp.directory", OdooDomain.Where( "name", _folder ) ) ).Unwrap();
		return ids.Count > 0 ? ids[ 0 ] : null;
	}

	private async Task<OdooRow?> EntryAsync( string name )
	{
		var ids = ( await OdooRpc.SearchAsync( "hp.entry",
			OdooDomain.Where( "windows_complete_name", $@"root\{_folder}\{name}" ) ) ).Unwrap();
		if( ids.Count == 0 )
			return null;
		return ( await OdooRpc.ReadAsync( "hp.entry", ids,
			[ "name", "deleted", "latest_version_id", "version_ids", "checkout_user" ] ) ).Unwrap()[ 0 ];
	}

	private async Task<OdooRow> RequireEntryAsync( string name )
		=> await EntryAsync( name ) ?? throw new Xunit.Sdk.XunitException( $"no entry for {name}\n{_reporter.Dump()}" );

	private async Task<int> EntryIdAsync( string name ) => Convert.ToInt32( ( await RequireEntryAsync( name ) )[ "id" ] );

	private async Task<int> HolderAsync( string name ) => OdooValues.Many2OneId( ( await RequireEntryAsync( name ) )[ "checkout_user" ] ) ?? 0;

	private async Task<int> VersionCountAsync( string name ) => ( await RequireEntryAsync( name ) ).RefIds( "version_ids" ).Count;

	private static string Sha1( string path ) => Convert.ToHexString( SHA1.HashData( File.ReadAllBytes( path ) ) ).ToLowerInvariant();

	/// <summary>Alice commits <paramref name="name"/> (a new file) and Bob gets a copy of it.</summary>
	private async Task<int> SharedFileAsync( string name, string contents )
	{
		await AsAliceAsync();
		await _service.CommitAsync( [ Hack( Write( AliceVault, name, contents ) ) ] );
		int id = await EntryIdAsync( name );
		await AsBobAsync();
		await _service.GetLatestAsync( [ id ] );
		Assert.Equal( contents, File.ReadAllText( PathIn( BobVault, name ) ) );
		return id;
	}

	/// <summary>A commit the way the client builds one, but without its "is this checked out to me" filter,
	/// as an older or modified client would send it. Returns the server's answer.</summary>
	private static async Task<(bool Committed, string? Error)> CommitAroundTheClientAsync( string path, bool existing )
	{
		HpPDMCommit commit = new() { node_by = OdooDefaults.Instance.MyNode?.id };
		await commit.CreateCommitAsync();
		Assert.True( await PdmCommitOperations.CommitRecordAsync( Hack( path ), commit, inOdoo: existing ) );
		return await commit.ServerCommit();
	}

	private void Clear() { while( _reporter.Lines.TryDequeue( out _ ) ) { } while( _reporter.Notices.TryDequeue( out _ ) ) { } }

	#endregion

	#region someone else's file

	[OdooFact]
	public async Task Bob_CannotTake_Release_Delete_OrCommit_AFileAliceHasCheckedOut()
	{
		int id = await SharedFileAsync( "held.txt", "alice v1\n" );
		await AsAliceAsync();
		await _service.CheckoutAsync( [ id ] );
		Assert.Equal( _aliceId, await HolderAsync( "held.txt" ) );

		await AsBobAsync();
		Clear();
		await _service.CheckoutAsync( [ id ] );
		Assert.Contains( _reporter.Errors, e => e.Contains( "Cannot check out held.txt: it is checked out to" ) );
		await _service.UnCheckoutAsync( [ id ] );
		Assert.Contains( _reporter.Errors, e => e.Contains( "Cannot undo the check out of held.txt" ) );
		await _service.LogicalDeleteAsync( [ id ] );
		Assert.Contains( _reporter.Errors, e => e.Contains( "Could not mark held.txt deleted" ) );
		Assert.Equal( _aliceId, await HolderAsync( "held.txt" ) );
		Assert.Equal( false, ( await RequireEntryAsync( "held.txt" ) )[ "deleted" ] );

		// through the client: refused before anything is sent
		string bobs = Write( BobVault, "held.txt", "bob's change\n" );
		await _service.CommitAsync( [ Hack( bobs ) ] );
		Assert.Contains( _reporter.Errors, e => e.Contains( "not checked out to current user" ) );
		// around the client: refused by the server
		var (committed, error) = await CommitAroundTheClientAsync( bobs, existing: true );
		Assert.False( committed );
		Assert.Contains( "checked out to another user", error );
		Assert.Equal( 1, await VersionCountAsync( "held.txt" ) );
		Assert.Equal( _aliceId, await HolderAsync( "held.txt" ) );
	}

	[OdooFact]
	public async Task Bob_CannotCommit_AFileNobodyHasCheckedOut_EvenAroundTheClient()
	{
		await SharedFileAsync( "free.txt", "v1\n" );
		await AsBobAsync();
		string bobs = Write( BobVault, "free.txt", "bob's unreviewed change\n" );

		Clear();
		await _service.CommitAsync( [ Hack( bobs ) ] );
		Assert.Contains( _reporter.Errors, e => e.Contains( "not checked out to current user" ) );

		var (committed, error) = await CommitAroundTheClientAsync( bobs, existing: true );
		Assert.False( committed, "the server accepted a new version of a file nobody checked out" );
		Assert.Contains( "not checked out", error );
		Assert.Equal( 1, await VersionCountAsync( "free.txt" ) );
	}

	[OdooFact]
	public async Task NewFile_WithAnExistingName_CannotReplaceTheVaultFile()
	{
		// Bob has his own "dup.txt" that he never got from the vault, and commits it as new
		await SharedFileAsync( "dup.txt", "alice's\n" );
		await AsBobAsync();
		string bobs = Write( BobVault, "dup.txt", "bob's own file of the same name\n" );
		var (committed, error) = await CommitAroundTheClientAsync( bobs, existing: false );
		Assert.False( committed );
		Assert.Contains( "already exists", error );
		Assert.Equal( 1, await VersionCountAsync( "dup.txt" ) );
	}

	#endregion

	#region local work

	[OdooFact]
	public async Task GetLatest_KeepsUncommittedChanges_ToACheckedOutFile()
	{
		int id = await SharedFileAsync( "work.txt", "v1\n" );
		await AsAliceAsync();
		await _service.CheckoutAsync( [ id ] );
		string mine = Write( AliceVault, "work.txt", "two hours of work\n" );

		await _service.GetLatestAsync( [ id ] );

		Assert.Equal( "two hours of work\n", File.ReadAllText( mine ) );
	}

	[OdooFact]
	public async Task GetLatest_KeepsLocalChanges_ToAFileThatIsNotCheckedOut()
	{
		int id = await SharedFileAsync( "draft.txt", "v1\n" );
		await AsBobAsync();
		string bobs = Write( BobVault, "draft.txt", "bob edited without checking out\n" );

		await _service.GetLatestAsync( [ id ] );

		Assert.Equal( "bob edited without checking out\n", File.ReadAllText( bobs ) );
		Assert.Contains( _reporter.Lines, l => l.Contains( "draft.txt" ) && l.Contains( "local changes" ) );
	}

	[OdooFact]
	public async Task GetLatest_UpdatesAnOlderUnchangedCopy()
	{
		int id = await SharedFileAsync( "older.txt", "v1\n" );
		await AsAliceAsync();
		await _service.CheckoutAsync( [ id ] );
		await _service.CommitAsync( [ Hack( Write( AliceVault, "older.txt", "v2\n" ) ) ] );
		Assert.Equal( 2, await VersionCountAsync( "older.txt" ) );

		await AsBobAsync();
		Assert.True( new FileInfo( PathIn( BobVault, "older.txt" ) ).IsReadOnly );
		Clear();
		await _service.GetLatestAsync( [ id ] );

		Assert.False( _reporter.Errors.Any(), _reporter.Dump() );
		Assert.Equal( "v2\n", File.ReadAllText( PathIn( BobVault, "older.txt" ) ) );
		Assert.True( new FileInfo( PathIn( BobVault, "older.txt" ) ).IsReadOnly );
	}

	[OdooFact]
	public async Task Checkout_ThenGetLatest_OfAStaleCopy_UpdatesIt()
	{
		// Bob checks out without getting latest first: his unchanged v1 is replaced by v2
		int id = await SharedFileAsync( "stale.txt", "v1\n" );
		await AsAliceAsync();
		await _service.CheckoutAsync( [ id ] );
		await _service.CommitAsync( [ Hack( Write( AliceVault, "stale.txt", "v2\n" ) ) ] );

		await AsBobAsync();
		await _service.CheckoutAsync( [ id ] );
		await _service.GetLatestAsync( [ id ] );
		Assert.Equal( "v2\n", File.ReadAllText( PathIn( BobVault, "stale.txt" ) ) );
	}

	#endregion

	#region names and situations the server must refuse cleanly

	[OdooTheory]
	[InlineData( "CON.txt" )]
	[InlineData( "colon:name.txt" )]
	[InlineData( "question?.txt" )]
	[InlineData( "trailing dot." )]
	public async Task Commit_OfANameWindowsCannotHold_IsRefused_AndLeavesNothing( string name )
	{
		if( OperatingSystem.IsWindows() )
			return; // these names cannot exist locally there in the first place
		await AsBobAsync();
		string good = Write( BobVault, "good.txt", "fine\n" );
		string bad = Write( BobVault, name, "bad name\n" );

		Clear();
		await _service.CommitAsync( [ Hack( good ), Hack( bad ) ] );

		Assert.Null( await EntryAsync( name ) );
		if( _reporter.Lines.Any( l => l.StartsWith( "SKIP" ) && l.Contains( name ) ) )
			return; // no file type at all ("trailing dot."): left out, and said so
		Assert.Contains( _reporter.Lines, l => l.StartsWith( "ERROR" ) );
		Assert.DoesNotContain( _reporter.Notices, n => n.EndsWith( "Completed!" ) );
		Assert.Null( await EntryAsync( "good.txt" ) ); // one commit: all or nothing
	}

	[OdooFact]
	public async Task Commit_OfTwoFilesDifferingOnlyInCase_IsRefused()
	{
		if( OperatingSystem.IsWindows() )
			return;
		await AsBobAsync();
		string upper = Write( BobVault, "Case.txt", "upper\n" );
		string lower = Write( BobVault, "case.txt", "lower\n" );

		Clear();
		await _service.CommitAsync( [ Hack( upper ), Hack( lower ) ] );

		Assert.Contains( _reporter.Lines, l => l.StartsWith( "ERROR" ) );
		Assert.Null( await EntryAsync( "Case.txt" ) );
		Assert.Null( await EntryAsync( "case.txt" ) );
	}

	[OdooFact]
	public async Task Commit_OfAFileDeletedAfterItWasPicked_FailsCleanly()
	{
		await AsBobAsync();
		string gone = Write( BobVault, "vanishing.txt", "here a moment\n" );
		HackFile picked = Hack( gone );
		File.Delete( gone );

		Clear();
		await _service.CommitAsync( [ picked ] ); // must not throw
		Assert.Null( await EntryAsync( "vanishing.txt" ) );
		Assert.Contains( _reporter.Errors, e => e.Contains( "no longer there" ) );
	}

	[OdooFact]
	public async Task PermanentDelete_ByAUser_IsRefused_AndLeavesTheFileWhole()
	{
		int id = await SharedFileAsync( "keep.txt", "keep me\n" );
		await AsBobAsync();
		Clear();
		bool deleted = await _service.PermanentDeleteAsync( [ id ] );
		Assert.False( deleted );
		await AsAdminAsync();
		var entry = await RequireEntryAsync( "keep.txt" );
		Assert.Single( entry.RefIds( "version_ids" ) );
		// the stored file is still there
		string again = Path.Combine( _root, "check" );
		HackDefaults.Instance.PwaPathAbsolute = again;
		await _service.GetLatestAsync( [ id ] );
		Assert.Equal( "keep me\n", File.ReadAllText( PathIn( again, "keep.txt" ) ) );
	}

	#endregion
}
