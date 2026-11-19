using System.Text.Json;


namespace HackPDM.Tests.Integration;

/// <summary>
/// Milestone 5.2 multi-user concurrency & load tests: verifies simultaneous directory/dependency
/// browsing, concurrent checkout race serialization between distinct engineers, and case-insensitive
/// entry uniqueness under concurrent creation.
/// </summary>
[Collection( OdooCollection.Name )]
public sealed class ConcurrencyLoadTests
{
	public ConcurrencyLoadTests() => OdooTestServer.ConfigureClient();

	private static async Task<int> EnsureUserAsync( string login, string name, string password )
	{
		var existing = ( await OdooRpc.SearchAsync( "res.users", OdooDomain.Where( "login", login ) ) ).Unwrap();
		if( existing.Count > 0 )
			return existing[ 0 ];
		return ( await OdooRpc.CreateAsync( "res.users", new Dictionary<string, object?>
		{
			[ "name" ] = name,
			[ "login" ] = login,
			[ "password" ] = password,
		} ) ).Unwrap();
	}

	private static async Task<Result<JsonElement>> CallAsAsync( int uid, string password, string model, string method, params object?[] args )
	{
		string db = OdooDefaults.Instance.OdooDb!;
		return await OdooRpc.CallAsync( "object", "execute", [ db, uid, password, model, method, .. args ] );
	}

	[OdooFact]
	public async Task ConcurrentReaders_DirectoryAndDependencyQueries_SucceedWithoutErrors()
	{
		await OdooTestServer.LoginAsync();

		// 10 concurrent engineering clients browsing the seeded directory tree and entries simultaneously
		Task<Result<JsonElement>>[] reads = Enumerable.Range( 0, 10 ).Select( i =>
		{
			return ( i % 3 ) switch
			{
				0 => OdooRpc.CallAsync( "object", "execute",
					[ OdooDefaults.Instance.OdooDb!, OdooDefaults.Instance.OdooId, OdooDefaults.Instance.OdooPass!, "hp.directory", "get_children_directories_by_id", 1, false ] ),
				1 => OdooRpc.CallAsync( "object", "execute",
					[ OdooDefaults.Instance.OdooDb!, OdooDefaults.Instance.OdooId, OdooDefaults.Instance.OdooPass!, "hp.directory", "get_entries", new object[] { 1, false } ] ),
				_ => OdooRpc.CallAsync( "object", "execute",
					[ OdooDefaults.Instance.OdooDb!, OdooDefaults.Instance.OdooId, OdooDefaults.Instance.OdooPass!, "hp.entry", "search_read", Array.Empty<object>(), new[] { "id", "name", "latest_version_id" } ] ),
			};
		} ).ToArray();

		Result<JsonElement>[] results = await Task.WhenAll( reads );
		Assert.All( results, r => Assert.True( r.Ok, r.Error ) );
	}

	[OdooFact]
	public async Task ConcurrentCheckoutRace_OnlyOneEngineerHoldsTheEntry()
	{
		await OdooTestServer.LoginAsync();
		string tag = Guid.NewGuid().ToString( "N" )[ ..8 ];
		string pass = $"RacePass!{tag}";
		int alice = await EnsureUserAsync( $"race_alice_{tag}", "Race Alice", pass );
		int bob = await EnsureUserAsync( $"race_bob_{tag}", "Race Bob", pass );

		int nodeId = ( await OdooRpc.CreateAsync( "hp.node", new Dictionary<string, object?> { [ "name" ] = $"race-node-{tag}" } ) ).Unwrap();
		int dirId = ( await OdooRpc.CreateAsync( "hp.directory", new Dictionary<string, object?> { [ "name" ] = $"RaceDir_{tag}", [ "parent_id" ] = 1 } ) ).Unwrap();
		int entryId = ( await OdooRpc.CreateAsync( "hp.entry", new Dictionary<string, object?> { [ "name" ] = "contested.txt", [ "dir_id" ] = dirId } ) ).Unwrap();
		int versionId = ( await OdooRpc.CreateAsync( "hp.version", new Dictionary<string, object?>
		{
			[ "entry_id" ] = entryId,
			[ "dir_id" ] = dirId,
			[ "file_contents" ] = Convert.ToBase64String( "contested payload"u8.ToArray() ),
		} ) ).Unwrap();

		// Both engineers attempt to check out the exact same version concurrently
		Task<Result<JsonElement>> aliceTask = CallAsAsync( alice, pass, "hp.version", "checkout_entry", versionId, nodeId );
		Task<Result<JsonElement>> bobTask = CallAsAsync( bob, pass, "hp.version", "checkout_entry", versionId, nodeId );
		Result<JsonElement>[] race = await Task.WhenAll( aliceTask, bobTask );

		int winners = race.Count( r => r.Ok && r.Value.ValueKind == JsonValueKind.True );
		Assert.Equal( 1, winners );

		var rows = ( await OdooRpc.ReadAsync( "hp.entry", [ entryId ], [ "checkout_user", "is_checked_out" ] ) ).Unwrap();
		Assert.True( Convert.ToBoolean( rows[ 0 ][ "is_checked_out" ] ) );
		int? holder = OdooValues.Many2OneId( rows[ 0 ][ "checkout_user" ] );
		Assert.True( holder == alice || holder == bob, $"Unexpected holder: {holder}" );
	}

	[OdooFact]
	public async Task ConcurrentCaseInsensitiveCreate_EnforcesUniqueConstraint()
	{
		await OdooTestServer.LoginAsync();
		string tag = Guid.NewGuid().ToString( "N" )[ ..8 ];
		int dirId = ( await OdooRpc.CreateAsync( "hp.directory", new Dictionary<string, object?> { [ "name" ] = $"UniqDir_{tag}", [ "parent_id" ] = 1 } ) ).Unwrap();

		string[] names = [ "Bracket.SLDPRT", "bracket.sldprt", "BRACKET.SLDPRT" ];
		Task<Result<int>>[] creates = names.Select( name =>
			OdooRpc.CreateAsync( "hp.entry", new Dictionary<string, object?> { [ "name" ] = name, [ "dir_id" ] = dirId } ) ).ToArray();

		Result<int>[] results = await Task.WhenAll( creates );
		Assert.Equal( 1, results.Count( r => r.Ok ) );

		var stored = ( await OdooRpc.SearchAsync( "hp.entry", OdooDomain.Where( "dir_id", dirId ) ) ).Unwrap();
		Assert.Single( stored );
	}
}
