using System.Collections;
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

internal sealed class InMemorySettingsProvider : ISettingsProvider
{
	private readonly ConcurrentDictionary<string, object?> _values = new();
	public T? Get<T>( string key, T? defaultValue = default )
		=> _values.TryGetValue( key, out var v ) && v is T t ? t : defaultValue;
	public void Set<T>( string key, T value ) => _values[ key ] = value;
	public bool Contains( string key ) => _values.ContainsKey( key );
	public void Remove( string key ) => _values.TryRemove( key, out _ );
}

[CollectionDefinition( Name, DisableParallelization = true )]
public sealed class OdooCollection
{
	public const string Name = "Live Odoo server";
}

/// <summary>
/// Exercises the real JSON-RPC client (<see cref="OdooClient"/>) against an Odoo 16.0 server
/// with the hackpdm addon installed.
/// </summary>
[Collection( OdooCollection.Name )]
public class OdooIntegrationTests
{
	private const string Settings = "hp.settings";
	private const string Category = "hp.category";

	public OdooIntegrationTests()
	{
		if( !OdooTestServer.IsConfigured )
			return;

		var defaults = OdooDefaults.Instance;
		defaults.Settings ??= new InMemorySettingsProvider();
		defaults.OdooUrl = OdooTestServer.Url!.TrimEnd( '/' );
		defaults.OdooDb = OdooTestServer.Db;
		defaults.OdooUser = OdooTestServer.User;
		defaults.OdooPass = OdooTestServer.Password;
	}

	private static async Task<int> LoginAsync()
	{
		int uid = await OdooClient.LoginJson( 30_000 ) ?? 0;
		Assert.True( uid > 0, $"Odoo login failed: {OdooClient.LatestException}" );
		return uid;
	}

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
			int uid = await OdooClient.LoginJson( 30_000 ) ?? 0;
			Assert.Equal( 0, uid );
		}
		finally
		{
			OdooDefaults.Instance.OdooPass = good;
			await LoginAsync();
		}
	}

	[OdooFact]
	public async Task HackPdmAddon_SeedSettings_AreReadable()
	{
		await LoginAsync();

		ArrayList ids = await OdooClient.SearchAsync( Settings, [ new ArrayList { new ArrayList { "name", "=", "client_version" } } ] );
		Assert.True( ids.Count == 1, $"expected one client_version setting, got {ids.Count}: {OdooClient.LatestException}" );

		ArrayList rows = await OdooClient.ReadAsync( Settings, ids, [ "name", "type", "char_value" ] );
		var row = Assert.IsType<Hashtable>( Assert.Single( rows ) );
		Assert.Equal( "char", row[ "type" ] );
		Assert.False( string.IsNullOrEmpty( row[ "char_value" ] as string ) );
	}

	[OdooFact]
	public async Task HackPdmAddon_Category_CreateReadUpdateDelete()
	{
		await LoginAsync();
		string name = $"ci-category-{Guid.NewGuid():N}";

		int id = await OdooClient.CreateAsync( Category, new Hashtable { { "name", name }, { "track_version", true } } );
		Assert.True( id > 0, $"create failed: {OdooClient.LatestException}" );
		try
		{
			ArrayList rows = await OdooClient.ReadAsync( Category, [ id ], [ "name", "track_version" ] );
			var row = Assert.IsType<Hashtable>( Assert.Single( rows ) );
			Assert.Equal( name, row[ "name" ] );
			Assert.Equal( true, row[ "track_version" ] );

			Assert.True( await OdooClient.UpdateAsync( Category, id, new Hashtable { { "track_version", false } } ),
				$"write failed: {OdooClient.LatestException}" );
			rows = await OdooClient.ReadAsync( Category, [ id ], [ "track_version" ] );
			Assert.Equal( false, ( (Hashtable)rows[ 0 ]! )[ "track_version" ] );
		}
		finally
		{
			Assert.True( await OdooClient.DeleteAsync( Category, [ new ArrayList { id } ] ),
				$"unlink failed: {OdooClient.LatestException}" );
		}

		int remaining = await OdooClient.SearchCountAsync( Category, [ new ArrayList { new ArrayList { "id", "=", id } } ] );
		Assert.Equal( 0, remaining );
	}
}
