using System.Collections.Concurrent;
using System.Security.Cryptography;

using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Operations;
using HackPDM.Mvvm.Models.Common;
using HackPDM.Mvvm.Models.Hack;
using HackPDM.Shared.GlobalData;
using HackPDM.Tests.Core;

namespace HackPDM.Tests.Integration;

/// <summary>Collects everything a workflow reports instead of showing a status window.</summary>
internal sealed class RecordingReporter : IOperationReporter
{
	public ConcurrentQueue<string> Lines { get; } = new();
	public ConcurrentQueue<string> Notices { get; } = new();
	public IEnumerable<string> Errors => Lines.Where( l => l.StartsWith( $"{StatusMessage.ERROR}:" ) );

	public void Log( StatusMessage status, string message ) => Lines.Enqueue( $"{status}: {message}" );
	public void ShowWindow() { }
	public CancellationTokenSource BeginOperation() => new();
	public void Cancel() { }
	public void SetTotal( int total ) { }
	public void Advance( int processed, int skipped = 0 ) { }
	public void SetProcessed( int processed ) { }
	public void Complete() { }
	public Task NotifyAsync( string message, string caption = "Status" ) { Notices.Enqueue( $"{caption}: {message}" ); return Task.CompletedTask; }

	public string Dump() => string.Join( "\n", Lines ) + "\n-- notices --\n" + string.Join( "\n", Notices );
}

/// <summary>
/// The user-facing PDM workflows end to end, through <see cref="PdmWorkflowService"/> (the same
/// code the file manager calls) against a live Odoo server and a temporary working folder:
/// commit, new versions, get latest, checkout / undo checkout, delete / restore, permanent
/// delete and historical versions. Each test works in its own uniquely named folder.
/// </summary>
[Collection( OdooCollection.Name )]
public class PdmWorkflowTests : IAsyncLifetime
{
	private readonly string _vault = Path.Combine( Path.GetTempPath(), $"hackpdm-vault-{Guid.NewGuid():N}" );
	private readonly string _otherVault = Path.Combine( Path.GetTempPath(), $"hackpdm-vault2-{Guid.NewGuid():N}" );
	private readonly string _folder = $"WF{Guid.NewGuid():N}"[ ..14 ];
	private readonly HackPDM.Abstractions.ISettingsProvider? _previousSettings = HackDefaults.Instance.SettingsProvider;
	private readonly string _previousPwa = HackDefaults.Instance.PwaPathAbsolute;
	private readonly RecordingReporter _reporter = new();
	private readonly PdmWorkflowService _service;
	private int _me;

	public PdmWorkflowTests() => _service = new PdmWorkflowService( _reporter );

	public async Task InitializeAsync()
	{
		if( !OdooTestServer.IsConfigured )
			return;
		OdooTestServer.ConfigureClient();
		Directory.CreateDirectory( _vault );
		HackDefaults.Instance.SettingsProvider = new InMemorySettingsProvider();
		HackDefaults.Instance.PwaPathAbsolute = _vault;

		_me = await OdooTestServer.LoginAsync();
		OdooDefaults.Instance.OdooId = _me;
		await OdooDefaults.Instance.InitializeAsync();
		Assert.NotNull( OdooDefaults.Instance.MyNode );
		_service.PreloadMetadata();
	}

	public Task DisposeAsync()
	{
		HackDefaults.Instance.SettingsProvider = _previousSettings;
		HackDefaults.Instance.PwaPathAbsolute = _previousPwa;
		foreach( string dir in new[] { _vault, _otherVault } )
			try { Directory.Delete( dir, true ); } catch { }
		return Task.CompletedTask;
	}

	#region helpers

	private string LocalPath( string vault, params string[] relative ) => Path.Combine( [ vault, _folder, .. relative ] );

	private string WriteFile( string relative, string contents )
	{
		string path = LocalPath( _vault, relative.Split( '/' ) );
		Directory.CreateDirectory( Path.GetDirectoryName( path )! );
		File.WriteAllText( path, contents );
		return path;
	}

	private static string Sha1( string path ) => Convert.ToHexString( SHA1.HashData( File.ReadAllBytes( path ) ) ).ToLowerInvariant();

	private static HackFile Hack( string path ) => HackFile.GetFromPath( path ) ?? throw new InvalidOperationException( path );

	private async Task CommitAsync( params string[] paths )
	{
		await _service.CommitAsync( paths.Select( Hack ) );
	}

	/// <summary>The entry for <paramref name="relative"/> (under this test's folder) as a field map, or null.</summary>
	private async Task<OdooRow?> EntryAsync( string relative, bool includeDeleted = false )
	{
		string windowsName = @"root\" + _folder + @"\" + relative.Replace( '/', '\\' );
		OdooDomain domain = OdooDomain.Where( "windows_complete_name", windowsName );
		if( !includeDeleted )
			domain = domain.And( "deleted", false );
		IReadOnlyList<int> ids = ( await OdooRpc.SearchAsync( "hp.entry", domain ) ).Unwrap();
		if( ids.Count == 0 )
			return null;
		Assert.True( ids.Count == 1, $"{ids.Count} entries named {windowsName}" );
		var rows = await OdooRpc.ReadAsync( "hp.entry", ids,
			[ "name", "deleted", "dir_id", "latest_version_id", "version_ids", "checkout_user", "checkout_node", "is_checked_out", "directory_complete_name" ] );
		return rows.Unwrap()[ 0 ];
	}

	private async Task<OdooRow> RequireEntryAsync( string relative, bool includeDeleted = false )
		=> await EntryAsync( relative, includeDeleted ) ?? throw new Xunit.Sdk.XunitException( $"no entry for {relative}\n{_reporter.Dump()}" );

	private static int Id( object? many2one ) => OdooValues.Many2OneId( many2one ) ?? 0;

	private static async Task<OdooRow> VersionAsync( int id, params string[] fields )
	{
		var rows = await OdooRpc.ReadAsync( "hp.version", [ id ], [ "name", "checksum", "file_size", "entry_id", .. fields ] );
		return rows.Unwrap()[ 0 ];
	}

	private void AssertNoErrors() => Assert.False( _reporter.Errors.Any(), _reporter.Dump() );

	private void UseVault( string vault )
	{
		Directory.CreateDirectory( vault );
		HackDefaults.Instance.PwaPathAbsolute = vault;
	}

	#endregion

	[OdooFact]
	public async Task Commit_NewFiles_CreatesDirectoriesEntriesAndVersions()
	{
		string a = WriteFile( "Sub/a.txt", "alpha\n" );
		string b = WriteFile( "Sub/Deeper/b.txt", "bravo bravo\n" );

		await CommitAsync( a, b );

		AssertNoErrors();
		var entryA = await RequireEntryAsync( "Sub/a.txt" );
		var entryB = await RequireEntryAsync( "Sub/Deeper/b.txt" );
		Assert.Equal( $"root / {_folder} / Sub", entryA[ "directory_complete_name" ] );
		Assert.Equal( $"root / {_folder} / Sub / Deeper", entryB[ "directory_complete_name" ] );

		var versionA = await VersionAsync( Id( entryA[ "latest_version_id" ] ) );
		Assert.Equal( Sha1( a ), versionA[ "checksum" ] );
		Assert.Equal( 6L, Convert.ToInt64( versionA[ "file_size" ] ) );
		Assert.Single( entryA.RefIds( "version_ids" ) );
		// nothing is left checked out by a first commit
		Assert.Equal( false, entryA[ "is_checked_out" ] );
	}

	[OdooFact]
	public async Task GetLatest_IntoAnotherWorkingFolder_ReproducesTheFiles()
	{
		string a = WriteFile( "Sub/a.txt", "alpha\n" );
		string b = WriteFile( "Sub/Deeper/b.txt", "bravo bravo\n" );
		await CommitAsync( a, b );
		AssertNoErrors();
		List<int> ids =
		[
			Convert.ToInt32( ( await RequireEntryAsync( "Sub/a.txt" ) )[ "id" ] ),
			Convert.ToInt32( ( await RequireEntryAsync( "Sub/Deeper/b.txt" ) )[ "id" ] ),
		];

		UseVault( _otherVault );
		await _service.GetLatestAsync( ids );

		AssertNoErrors();
		Assert.Equal( "alpha\n", File.ReadAllText( LocalPath( _otherVault, "Sub", "a.txt" ) ) );
		Assert.Equal( "bravo bravo\n", File.ReadAllText( LocalPath( _otherVault, "Sub", "Deeper", "b.txt" ) ) );
		Assert.Contains( _reporter.Notices, n => n.Contains( "Completed! 2 downloaded" ) );

		// a second get latest has nothing new to fetch
		await _service.GetLatestAsync( ids );
		Assert.Contains( _reporter.Notices, n => n.Contains( "0 downloaded, 2 already up to date" ) );
	}

	[OdooFact]
	public async Task LargeFiles_GoInPieces_ThroughCommitGetLatestNewVersionAndHistory()
	{
		long inline = ContentTransfer.InlineLimitBytes;
		int chunk = ContentTransfer.ChunkBytes;
		// small limits, so a test-sized file takes several pieces (and a last, shorter one)
		ContentTransfer.InlineLimitBytes = 64 * 1024;
		ContentTransfer.ChunkBytes = 100 * 1024;
		try
		{
			byte[] first = RandomNumberGenerator.GetBytes( 350 * 1024 + 7 );
			string a = WriteFile( "big.txt", "" );
			File.WriteAllBytes( a, first );
			await CommitAsync( a );
			AssertNoErrors();

			var entry = await RequireEntryAsync( "big.txt" );
			int entryId = Convert.ToInt32( entry[ "id" ] );
			int firstVersion = Id( entry[ "latest_version_id" ] );
			var stored = await VersionAsync( firstVersion );
			Assert.Equal( Sha1( a ), stored[ "checksum" ] );
			Assert.Equal( (long)first.Length, Convert.ToInt64( stored[ "file_size" ] ) );
			// it really went through an upload, not inline
			Assert.NotEmpty( ( await OdooRpc.SearchAsync( "hp.upload",
				OdooDomain.Where( "size", first.Length ).And( "state", "used" ).And( "checksum", Sha1( a ) ) ) ).Unwrap() );

			// get latest into another working folder: in pieces, checked, moved into place
			UseVault( _otherVault );
			await _service.GetLatestAsync( [ entryId ] );
			AssertNoErrors();
			Assert.Equal( first, File.ReadAllBytes( LocalPath( _otherVault, "big.txt" ) ) );
			// the temporary download file was moved, not left behind
			Assert.DoesNotContain( Directory.EnumerateFiles( Path.GetTempPath(), "hackpdm-*.part" ), f => File.GetLastWriteTimeUtc( f ) > DateTime.UtcNow.AddMinutes( -5 ) );

			// a new version of the existing file
			UseVault( _vault );
			await _service.CheckoutAsync( [ entryId ] );
			byte[] second = RandomNumberGenerator.GetBytes( 200 * 1024 );
			File.SetAttributes( a, FileAttributes.Normal );
			File.WriteAllBytes( a, second );
			await CommitAsync( a );
			AssertNoErrors();
			entry = await RequireEntryAsync( "big.txt" );
			Assert.Equal( 2, entry.RefIds( "version_ids" ).Count );
			Assert.Equal( Sha1( a ), ( await VersionAsync( Id( entry[ "latest_version_id" ] ) ) )[ "checksum" ] );

			// the first version, fetched on its own (into memory, in pieces)
			var plan = await _service.PlanVersionDownloadAsync( firstVersion );
			Assert.NotNull( plan );
			UseVault( _otherVault );
			File.SetAttributes( LocalPath( _otherVault, "big.txt" ), FileAttributes.Normal );
			Assert.Empty( await _service.DownloadVersionsAsync( plan.VersionIds ) );
			Assert.Equal( first, File.ReadAllBytes( LocalPath( _otherVault, "big.txt" ) ) );
		}
		finally
		{
			ContentTransfer.InlineLimitBytes = inline;
			ContentTransfer.ChunkBytes = chunk;
		}
	}

	[OdooFact]
	public async Task LargeUpload_ContinuesFromWhatTheServerHas_AndRefusesADamagedFile()
	{
		int chunk = ContentTransfer.ChunkBytes;
		ContentTransfer.ChunkBytes = 1000;
		try
		{
			byte[] data = RandomNumberGenerator.GetBytes( 3500 );
			using MemoryStream stream = new( data );
			Result<int> uploaded = await ContentTransfer.UploadAsync( stream );
			Assert.True( uploaded.Ok, uploaded.Error );
			var row = ( await OdooRpc.ReadAsync( "hp.upload", [ uploaded.Value ], [ "state", "received", "size" ] ) ).Unwrap()[ 0 ];
			Assert.Equal( "done", row[ "state" ] );
			Assert.Equal( 3500, row.Int( "received" ) );

			// a piece sent past a gap (as after a lost call) is not written: the server says where to go on
			string sha1 = Convert.ToHexStringLower( SHA1.HashData( data ) );
			var begun = ( await OdooRpc.ExecuteAsync<OdooRow>( "hp.upload", "upload_begin", [ data.Length, sha1 ] ) ).Unwrap();
			int id = begun.Int( "id" ) ?? 0;
			Assert.Equal( 0, ( await OdooRpc.ExecuteAsync<int>( "hp.upload", "upload_chunk", [ id, 1000, Convert.ToBase64String( data, 1000, 1000 ) ] ) ).Unwrap() );
			Assert.Equal( 1000, ( await OdooRpc.ExecuteAsync<int>( "hp.upload", "upload_chunk", [ id, 0, Convert.ToBase64String( data, 0, 1000 ) ] ) ).Unwrap() );
			// a damaged file is not accepted
			Assert.Equal( 3500, ( await OdooRpc.ExecuteAsync<int>( "hp.upload", "upload_chunk", [ id, 1000, Convert.ToBase64String( new byte[ 2500 ] ) ] ) ).Unwrap() );
			Assert.False( ( await OdooRpc.ExecuteAsync<bool>( "hp.upload", "upload_finish", [ id ] ) ).Unwrap() );
		}
		finally
		{
			ContentTransfer.ChunkBytes = chunk;
		}
	}

	[OdooFact]
	public async Task Checkout_ThenUndoCheckout_SetsAndClearsTheOwner()
	{
		string a = WriteFile( "a.txt", "alpha\n" );
		await CommitAsync( a );
		int entryId = Convert.ToInt32( ( await RequireEntryAsync( "a.txt" ) )[ "id" ] );

		await _service.CheckoutAsync( [ entryId ] );
		var checkedOut = await RequireEntryAsync( "a.txt" );
		Assert.True( true.Equals( checkedOut[ "is_checked_out" ] ), _reporter.Dump() );
		Assert.Equal( _me, Id( checkedOut[ "checkout_user" ] ) );
		Assert.Equal( OdooDefaults.Instance.MyNode!.id, Id( checkedOut[ "checkout_node" ] ) );

		await _service.UnCheckoutAsync( [ entryId ] );
		var released = await RequireEntryAsync( "a.txt" );
		Assert.Equal( false, released[ "is_checked_out" ] );
		Assert.Equal( 0, Id( released[ "checkout_user" ] ) );
		AssertNoErrors();
	}

	[OdooFact]
	public async Task Commit_ChangedFile_WhenCheckedOut_AddsAVersion_OtherwiseIsRefused()
	{
		string a = WriteFile( "a.txt", "version one\n" );
		await CommitAsync( a );
		var entry = await RequireEntryAsync( "a.txt" );
		int entryId = Convert.ToInt32( entry[ "id" ] );
		int firstVersion = Id( entry[ "latest_version_id" ] );

		// not checked out: the change must not reach the server
		File.WriteAllText( a, "version two, not checked out\n" );
		await CommitAsync( a );
		Assert.True( _reporter.Errors.Any( e => e.Contains( "not checked out" ) ), _reporter.Dump() );
		entry = await RequireEntryAsync( "a.txt" );
		Assert.Single( entry.RefIds( "version_ids" ) );

		// checked out: a second version, now the latest
		await _service.CheckoutAsync( [ entryId ] );
		File.WriteAllText( a, "version two\n" );
		while( _reporter.Lines.TryDequeue( out _ ) ) { }
		await CommitAsync( a );
		AssertNoErrors();

		entry = await RequireEntryAsync( "a.txt" );
		Assert.Equal( 2, entry.RefIds( "version_ids" ).Count );
		int latest = Id( entry[ "latest_version_id" ] );
		Assert.NotEqual( firstVersion, latest );
		Assert.Equal( Sha1( a ), ( await VersionAsync( latest ) )[ "checksum" ] );

		// the earlier version is still there and can be fetched on its own
		var plan = await _service.PlanVersionDownloadAsync( firstVersion );
		Assert.NotNull( plan );
		Assert.False( plan.IsLatest );
		UseVault( _otherVault );
		Assert.Empty( await _service.DownloadVersionsAsync( plan.VersionIds ) );
		string historical = LocalPath( _otherVault, "a.txt" );
		Assert.True( File.Exists( historical ), $"versions [{string.Join( ",", plan.VersionIds.Cast<object>() )}]; written: "
			+ string.Join( ", ", Directory.EnumerateFiles( _otherVault, "*", SearchOption.AllDirectories ) ) + $"; in first vault: "
			+ string.Join( ", ", Directory.EnumerateFiles( _vault, "*", SearchOption.AllDirectories ) ) );
		Assert.Equal( "version one\n", File.ReadAllText( historical ) );
	}

	[OdooFact]
	public async Task Commit_UnchangedFile_DoesNotAddAVersion()
	{
		string a = WriteFile( "a.txt", "same\n" );
		await CommitAsync( a );
		int entryId = Convert.ToInt32( ( await RequireEntryAsync( "a.txt" ) )[ "id" ] );
		await _service.CheckoutAsync( [ entryId ] );

		await CommitAsync( a );

		Assert.Single( ( await RequireEntryAsync( "a.txt" ) ).RefIds( "version_ids" ) );
	}

	[OdooFact]
	public async Task LogicalDelete_ThenRestore()
	{
		string a = WriteFile( "a.txt", "alpha\n" );
		await CommitAsync( a );
		var entry = await RequireEntryAsync( "a.txt" );
		int entryId = Convert.ToInt32( entry[ "id" ] );
		int dirId = Id( entry[ "dir_id" ] );

		await _service.LogicalDeleteAsync( [ entryId ] );
		Assert.Null( await EntryAsync( "a.txt" ) );
		Assert.Equal( true, ( await RequireEntryAsync( "a.txt", includeDeleted: true ) )[ "deleted" ] );

		await _service.RestoreDeletedInDirectoryAsync( dirId );
		Assert.Equal( false, ( await RequireEntryAsync( "a.txt" ) )[ "deleted" ] );
		AssertNoErrors();
	}

	[OdooFact]
	public async Task PermanentDelete_RemovesEntryAndVersions()
	{
		string a = WriteFile( "a.txt", "alpha\n" );
		await CommitAsync( a );
		var entry = await RequireEntryAsync( "a.txt" );
		int entryId = Convert.ToInt32( entry[ "id" ] );
		IReadOnlyList<int> versionIds = entry.RefIds( "version_ids" );

		Assert.True( await _service.PermanentDeleteAsync( [ entryId ] ), _reporter.Dump() );

		Assert.Null( await EntryAsync( "a.txt", includeDeleted: true ) );
		Assert.Empty( ( await OdooRpc.SearchAsync( "hp.version", OdooDomain.IdIn( versionIds ) ) ).Unwrap() );
		// the local file is the user's; deleting from the server leaves it alone
		Assert.True( File.Exists( a ) );
	}

	/// <summary>
	/// Copies the sample SolidWorks files into this test's folder. In the samples new.SLDASM uses
	/// new.SLDPRT and old.SLDASM; old.SLDASM uses new.SLDPRT; new.SLDPRT is derived from
	/// "tmp engine 2.SLDPRT", which isn't among the samples (a copy of old.SLDPRT stands in for it).
	/// </summary>
	private string CopySolidWorksSamples( bool withBasePart )
	{
		string dir = LocalPath( _vault, "Sw" );
		Directory.CreateDirectory( dir );
		foreach( string name in new[] { "new.SLDASM", "old.SLDASM", "new.SLDPRT" } )
			File.Copy( SwSamples.Old( name ), Path.Combine( dir, name ) );
		if( withBasePart )
			File.Copy( SwSamples.Old( "old.SLDPRT" ), Path.Combine( dir, "tmp engine 2.SLDPRT" ) );
		return dir;
	}

	private static EntryRow LocalRow( string path )
		=> new() { Id = 0, FullName = path, Type = Path.GetExtension( path ).TrimStart( '.' ).ToLowerInvariant(), ReprType = EntryReprType.Local };

	private static HashSet<int> Ids( object? many ) => [ .. OdooValues.Ids( many ) ];

	[OdooFact]
	public async Task Commit_SolidWorksAssembly_PullsInItsReferences_AndLinksTheExactFiles()
	{
		// a file with the same name as one of the parts, in another folder, committed first:
		// relationships used to be linked by file name alone and could land on it
		string decoy = WriteFile( "Decoy/new.SLDPRT", "not the part the assembly uses\n" );
		await CommitAsync( decoy );
		AssertNoErrors();
		int decoyVersion = Id( ( await RequireEntryAsync( "Decoy/new.SLDPRT" ) )[ "latest_version_id" ] );

		string dir = CopySolidWorksSamples( withBasePart: true );
		HashSet<HackFile> files = await _service.CollectCommitFilesAsync( [ LocalRow( Path.Combine( dir, "new.SLDASM" ) ) ] );
		Assert.True( files.Count == 4, $"collected [{string.Join( ", ", files.Select( f => f.Name ) )}]\n{_reporter.Dump()}" );
		Assert.Equal( [ "new.SLDASM", "new.SLDPRT", "old.SLDASM", "tmp engine 2.SLDPRT" ], files.Select( f => f.Name ).Order() );

		await _service.CommitAsync( files );
		AssertNoErrors();

		async Task<OdooRow> Latest( string name, params string[] fields )
			=> await VersionAsync( Id( ( await RequireEntryAsync( $"Sw/{name}" ) )[ "latest_version_id" ] ), [ "child_ids", "parent_ids", .. fields ] );
		var asm = await Latest( "new.SLDASM", "preview_image" );
		var oldAsm = await Latest( "old.SLDASM" );
		var part = await Latest( "new.SLDPRT", "version_property_ids" );
		var basePart = await Latest( "tmp engine 2.SLDPRT", "version_property_ids" );
		int I( OdooRow v ) => Convert.ToInt32( v[ "id" ] );

		Assert.Equal( new HashSet<int> { I( part ), I( oldAsm ) }, Ids( asm[ "child_ids" ] ) );
		Assert.Equal( new HashSet<int> { I( part ) }, Ids( oldAsm[ "child_ids" ] ) );
		Assert.Equal( new HashSet<int> { I( basePart ) }, Ids( part[ "child_ids" ] ) );
		Assert.DoesNotContain( decoyVersion, Ids( asm[ "child_ids" ] ) );
		Assert.Empty( Ids( ( await VersionAsync( decoyVersion, "parent_ids" ) )[ "parent_ids" ] ) );

		// the assembly's saved preview and the parts' custom properties come along
		Assert.False( asm[ "preview_image" ] is null or false, "assembly preview missing" );
		var props = ( await OdooRpc.ReadAsync( "hp.version.property", basePart.RefIds( "version_property_ids" ), [ "prop_name", "sw_config_name", "text_value" ] ) ).Unwrap();
		Assert.Contains( props, p => p.Text( "prop_name" ) == "PartNum" && p.Text( "text_value" ) == "X000545" );
	}

	[OdooFact]
	public async Task Commit_SolidWorksAssembly_WithAMissingReference_IsRefused()
	{
		string dir = CopySolidWorksSamples( withBasePart: false );

		HashSet<HackFile> files = await _service.CollectCommitFilesAsync( [ LocalRow( Path.Combine( dir, "new.SLDASM" ) ) ] );

		Assert.Empty( files );
		Assert.Contains( _reporter.Errors, e => e.Contains( "MissingFile" ) && e.Contains( "tmp engine 2.SLDPRT" ) );
		Assert.Contains( _reporter.Errors, e => e.Contains( "Commit Terminated" ) );
	}

	[OdooFact]
	public async Task CommitFolder_CommitsItsFilesAndReferences_ButNotLockFiles()
	{
		string dir = CopySolidWorksSamples( withBasePart: true );
		File.WriteAllText( Path.Combine( dir, "notes.txt" ), "notes\n" );
		File.WriteAllText( Path.Combine( dir, "~$new.SLDASM" ), "SolidWorks lock file" );

		List<HackFile>? files = await _service.CollectFolderCommitFilesAsync( dir );
		Assert.NotNull( files );
		Assert.Equal( [ "new.SLDASM", "new.SLDPRT", "notes.txt", "old.SLDASM", "tmp engine 2.SLDPRT" ],
			files.Select( f => f.Name ).Where( n => !n!.StartsWith( "~$" ) ).Distinct().Order() );

		await _service.CommitAsync( files );
		AssertNoErrors();

		foreach( string name in new[] { "new.SLDASM", "new.SLDPRT", "notes.txt", "old.SLDASM", "tmp engine 2.SLDPRT" } )
			await RequireEntryAsync( $"Sw/{name}" );
		Assert.Null( await EntryAsync( "Sw/~$new.SLDASM", includeDeleted: true ) );
	}

	[OdooFact]
	public async Task GetLatest_AndCheckout_OfAnAssembly_IncludeItsReferences()
	{
		string dir = CopySolidWorksSamples( withBasePart: true );
		await _service.CommitAsync( await _service.CollectCommitFilesAsync( [ LocalRow( Path.Combine( dir, "new.SLDASM" ) ) ] ) );
		AssertNoErrors();
		int asmEntry = Convert.ToInt32( ( await RequireEntryAsync( "Sw/new.SLDASM" ) )[ "id" ] );
		string[] all = [ "new.SLDASM", "new.SLDPRT", "old.SLDASM", "tmp engine 2.SLDPRT" ];

		UseVault( _otherVault );
		await _service.GetLatestAsync( [ asmEntry ] );
		AssertNoErrors();
		Assert.All( all, name => Assert.Equal(
			File.ReadAllBytes( Path.Combine( dir, name ) ), File.ReadAllBytes( LocalPath( _otherVault, "Sw", name ) ) ) );

		await _service.CheckoutAsync( [ asmEntry ] );
		AssertNoErrors();
		foreach( string name in all )
			Assert.Equal( _me, Id( ( await RequireEntryAsync( $"Sw/{name}" ) )[ "checkout_user" ] ) );

		await _service.UnCheckoutAsync( [ asmEntry ] );
		foreach( string name in all )
			Assert.Equal( false, ( await RequireEntryAsync( $"Sw/{name}" ) )[ "is_checked_out" ] );
	}

	[OdooFact]
	public async Task Checkout_LocalCopyIsWritableOnlyWhileCheckedOut()
	{
		string a = WriteFile( "a.txt", "alpha\n" );
		await CommitAsync( a );
		int entryId = Convert.ToInt32( ( await RequireEntryAsync( "a.txt" ) )[ "id" ] );

		await _service.CheckoutAsync( [ entryId ] );
		Assert.False( new FileInfo( a ).IsReadOnly );
		await _service.UnCheckoutAsync( [ entryId ] );
		Assert.True( new FileInfo( a ).IsReadOnly );
		await _service.CheckoutAsync( [ entryId ] );
		Assert.False( new FileInfo( a ).IsReadOnly );
	}

	[OdooFact]
	public async Task FileCheckedOutBySomeoneElse_CannotBeTakenReleasedOrCommitted()
	{
		string a = WriteFile( "a.txt", "alpha\n" );
		await CommitAsync( a );
		var entry = await RequireEntryAsync( "a.txt" );
		int entryId = Convert.ToInt32( entry[ "id" ] );

		// another user holds the file
		string login = $"other-{Guid.NewGuid():N}"[ ..16 ];
		int other = ( await OdooRpc.CreateAsync( "res.users", new Dictionary<string, object?> { [ "name" ] = login, [ "login" ] = login } ) ).Unwrap();
		Assert.True( other > 0 );
		var held = await OdooRpc.WriteAsync( "hp.entry", [ entryId ], new Dictionary<string, object?>
		{
			[ "checkout_user" ] = other, [ "checkout_node" ] = OdooDefaults.Instance.MyNode!.id, [ "is_checked_out" ] = true,
		} );
		Assert.True( held.Value, held.Error );

		await _service.CheckoutAsync( [ entryId ] );
		Assert.Equal( other, Id( ( await RequireEntryAsync( "a.txt" ) )[ "checkout_user" ] ) );

		await _service.UnCheckoutAsync( [ entryId ] );
		Assert.Equal( other, Id( ( await RequireEntryAsync( "a.txt" ) )[ "checkout_user" ] ) );

		File.SetAttributes( a, FileAttributes.Normal );
		File.WriteAllText( a, "my change\n" );
		await CommitAsync( a );
		Assert.Contains( _reporter.Errors, e => e.Contains( "not checked out to current user" ) );
		Assert.Single( ( await RequireEntryAsync( "a.txt" ) ).RefIds( "version_ids" ) );

		// the server refuses too, whatever the client does
		var refused = await OdooRpc.ExecuteAsync<bool>( "hp.version", "checkout_entry", [ Id( entry[ "latest_version_id" ] ), OdooDefaults.Instance.MyNode!.id! ] );
		Assert.False( refused.Ok );
		Assert.Contains( "already checked out", refused.Error );
	}
}
