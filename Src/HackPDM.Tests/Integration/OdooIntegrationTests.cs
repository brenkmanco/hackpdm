using System.Collections.Concurrent;

using HackPDM.Abstractions;
using HackPDM.Infrastructure.Odoo;

namespace HackPDM.Tests.Integration;

/// <summary>
/// Connection settings for a live Odoo server, read from environment variables.
/// Set by .github/workflows/odoo-integration.yml; when ODOO_URL is not set the tests are skipped.
/// </summary>
internal static class OdooTestServer
{
	public static string? Url => Environment.GetEnvironmentVariable( "ODOO_URL" );
	public static string Db => Environment.GetEnvironmentVariable( "ODOO_DB" ) ?? "hackpdm_ci";
	public static string User => Environment.GetEnvironmentVariable( "ODOO_USER" ) ?? "admin";
	public static string Password => Environment.GetEnvironmentVariable( "ODOO_PASSWORD" ) ?? "admin";
	public static bool IsConfigured => !string.IsNullOrWhiteSpace( Url );

	/// <summary>Points the shared <see cref="OdooDefaults.Instance"/> at the test server.</summary>
	public static void ConfigureClient()
	{
		if( !IsConfigured )
			return;

		var defaults = OdooDefaults.Instance;
		defaults.Settings ??= new InMemorySettingsProvider();
		defaults.OdooUrl = Url!.TrimEnd( '/' );
		defaults.OdooDb = Db;
		defaults.OdooUser = User;
		defaults.OdooPass = Password;
	}

	public static async Task<int> LoginAsync()
	{
		Result<int> login = await OdooRpc.LoginAsync( 30_000 );
		Assert.True( login.Ok, $"Odoo login failed: {login.Error}" );
		return login.Value;
	}
}

/// <summary>A [Fact] that is skipped unless a live Odoo server is configured.</summary>
public sealed class OdooFactAttribute : FactAttribute
{
	public OdooFactAttribute()
	{
		if( !OdooTestServer.IsConfigured )
			Skip = "ODOO_URL not set; live Odoo integration tests skipped.";
	}
}

/// <summary>A live-Odoo test that moves a very large file; it runs only when <c>HACKPDM_BIG_FILE_MB</c> gives the size in MB.</summary>
public sealed class BigFileFactAttribute : FactAttribute
{
	/// <summary>The file size to test with, in MB; 0 when the test is not enabled.</summary>
	public static int SizeMb => int.TryParse( Environment.GetEnvironmentVariable( "HACKPDM_BIG_FILE_MB" ), out int mb ) && mb > 0 ? mb : 0;

	public BigFileFactAttribute()
	{
		if( !OdooTestServer.IsConfigured )
			Skip = "ODOO_URL not set; live Odoo integration tests skipped.";
		else if( SizeMb == 0 )
			Skip = "HACKPDM_BIG_FILE_MB not set; the large-file test is skipped (it moves hundreds of MB).";
	}
}

internal sealed class InMemorySettingsProvider : ISettingsProvider
{
	private readonly ConcurrentDictionary<string, object?> _values = new();
	public T? Get<T>( string key, T? defaultValue = default )
		=> _values.TryGetValue( key, out var v ) && v is T t ? t : defaultValue;
	public void Set<T>( string key, T value ) => _values[ key ] = value;
	public bool Contains( string key ) => _values.ContainsKey( key );
	public void Remove( string key ) => _values.TryRemove( key, out _ );
}

/// <summary>A [Theory] that is skipped unless a live Odoo server is configured.</summary>
public sealed class OdooTheoryAttribute : TheoryAttribute
{
	public OdooTheoryAttribute()
	{
		if( !OdooTestServer.IsConfigured )
			Skip = "ODOO_URL not set; live Odoo integration tests skipped.";
	}
}

[CollectionDefinition( Name, DisableParallelization = true )]
public sealed class OdooCollection
{
	public const string Name = "Live Odoo server";
}

/// <summary>
/// Exercises the real JSON-RPC client (<see cref="OdooRpc"/>) against an Odoo 16.0 server
/// with the hackpdm addon installed.
/// </summary>
[Collection( OdooCollection.Name )]
public class OdooIntegrationTests
{
	private const string Settings = "hp.settings";
	private const string Category = "hp.category";

	public OdooIntegrationTests() => OdooTestServer.ConfigureClient();

	private static Task<int> LoginAsync() => OdooTestServer.LoginAsync();

	[OdooFact]
	public async Task Login_WithAdminCredentials_ReturnsUserId()
	{
		int uid = await LoginAsync();
		Assert.Equal( uid, OdooDefaults.Instance.OdooId );
	}

	[OdooFact]
	public async Task Login_WithWrongPassword_ReturnsZero()
	{
		string? good = OdooDefaults.Instance.OdooPass;
		try
		{
			OdooDefaults.Instance.OdooPass = "definitely-not-the-password";
			Result<int> login = await OdooRpc.LoginAsync( 30_000 );
			Assert.False( login.Ok );
			Assert.Equal( 0, login.Value );
		}
		finally
		{
			OdooDefaults.Instance.OdooPass = good;
			await LoginAsync();
		}
	}

	[OdooFact]
	public async Task ServerError_IsReportedWithOdooMessage()
	{
		await LoginAsync();

		Result<object?> result = await OdooRpc.ExecuteAsync<object?>( "hp.settings", "no_such_method", [] );

		Assert.False( result.Ok );
		// Odoo's JSON-RPC error has an integer code and the real text in data.message
		Assert.Contains( "no_such_method", result.Error );
	}

	[OdooFact]
	public async Task HackPdmAddon_SeedSettings_AreReadable()
	{
		await LoginAsync();

		var ids = await OdooRpc.SearchAsync( Settings, OdooDomain.Where( "name", "client_version" ) );
		Assert.True( ids.Value is { Count: 1 }, $"expected one client_version setting, got {ids}" );

		var rows = await OdooRpc.ReadAsync( Settings, ids.Value!, [ "name", "type", "char_value" ] );
		OdooRow row = Assert.Single( rows.Value! );
		Assert.Equal( "char", row.Text( "type" ) );
		Assert.False( string.IsNullOrEmpty( row.Text( "char_value" ) ) );
	}

	[OdooFact]
	public async Task HackPdmAddon_Category_CreateReadUpdateDelete()
	{
		await LoginAsync();
		string name = $"ci-category-{Guid.NewGuid():N}";

		var created = await OdooRpc.CreateAsync( Category, new Dictionary<string, object?> { [ "name" ] = name, [ "track_version" ] = true } );
		Assert.True( created.Value > 0, $"create failed: {created.Error}" );
		int id = created.Value;
		try
		{
			var rows = await OdooRpc.ReadAsync( Category, [ id ], [ "name", "track_version" ] );
			OdooRow row = Assert.Single( rows.Value! );
			Assert.Equal( name, row.Text( "name" ) );
			Assert.True( row.Bool( "track_version" ) );

			var written = await OdooRpc.WriteAsync( Category, [ id ], new Dictionary<string, object?> { [ "track_version" ] = false } );
			Assert.True( written.Value, $"write failed: {written.Error}" );
			rows = await OdooRpc.ReadAsync( Category, [ id ], [ "track_version" ] );
			Assert.False( rows.Value![ 0 ].Bool( "track_version" ) );
		}
		finally
		{
			var deleted = await OdooRpc.UnlinkAsync( Category, [ id ] );
			Assert.True( deleted.Value, $"unlink failed: {deleted.Error}" );
		}

		var remaining = await OdooRpc.SearchCountAsync( Category, OdooDomain.Where( "id", id ) );
		Assert.Equal( 0, remaining.Value );
	}
}
