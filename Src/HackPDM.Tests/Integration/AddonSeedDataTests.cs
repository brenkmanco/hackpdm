using System.Xml.Linq;


namespace HackPDM.Tests.Integration;

/// <summary>
/// Verifies that every record declared in the hackpdm addon's data file
/// (Odoo/addons/hackpdm/data/hp_settings_data.xml) is loaded into Odoo with the expected values.
/// </summary>
[Collection( OdooCollection.Name )]
public class AddonSeedDataTests
{
	private const string Module = "hackpdm";
	private const string DataFile = "Odoo/addons/hackpdm/data/hp_settings_data.xml";

	public AddonSeedDataTests() => OdooTestServer.ConfigureClient();

	// ---------------------------------------------------------------- helpers

	private static string FindRepoFile( string relativePath )
	{
		for( var dir = new DirectoryInfo( AppContext.BaseDirectory ); dir is not null; dir = dir.Parent )
		{
			string candidate = Path.Combine( dir.FullName, relativePath );
			if( File.Exists( candidate ) )
				return candidate;
		}
		throw new FileNotFoundException( $"could not locate {relativePath} above {AppContext.BaseDirectory}" );
	}

	/// <summary>(xml id, model) for every &lt;record&gt; in the data file.</summary>
	private static List<(string Id, string Model)> XmlRecords()
		=> XDocument.Load( FindRepoFile( DataFile ) )
			.Descendants( "record" )
			.Select( r => ( (string)r.Attribute( "id" )!, (string)r.Attribute( "model" )! ) )
			.ToList();

	/// <summary>Resolves an addon XML id to (model, res_id) through ir.model.data.</summary>
	private static async Task<(string Model, int ResId)?> ResolveXmlIdAsync( string xmlId )
	{
		var rows = await OdooRpc.SearchReadAsync( "ir.model.data",
			OdooDomain.Where( "module", Module ).And( "name", xmlId ), [ "model", "res_id" ] );
		if( rows.Value is not { Count: 1 } found )
			return null;
		return ( found[ 0 ].Text( "model" )!, found[ 0 ].Int( "res_id" ) ?? 0 );
	}

	private static async Task<int> IdOf( string xmlId )
	{
		var resolved = await ResolveXmlIdAsync( xmlId );
		Assert.True( resolved is not null, $"xml id {Module}.{xmlId} not found" );
		return resolved!.Value.ResId;
	}

	private static async Task<OdooRow> ReadAsync( string model, string xmlId, params string[] fields )
	{
		int id = await IdOf( xmlId );
		var rows = await OdooRpc.ReadAsync( model, [ id ], fields );
		Assert.True( rows.Value is { Count: 1 }, $"read {model}({id}) returned {rows.Value?.Count ?? 0} rows: {rows.Error}" );
		return rows.Value![ 0 ];
	}

	// ---------------------------------------------------------------- coverage of the whole file

	public static TheoryData<string, string> AllXmlRecords()
	{
		var data = new TheoryData<string, string>();
		foreach( var (id, model) in XmlRecords() )
			data.Add( id, model );
		return data;
	}

	[Fact]
	public void DataFile_DeclaresOnlyProductionSettingsRecords()
	{
		var records = XmlRecords();
		Assert.NotEmpty( records );
		Assert.Equal( records.Count, records.Select( r => r.Id ).Distinct().Count() );
		Assert.All( records, r => Assert.Equal( "hp.settings", r.Model ) );
	}

	[OdooTheory]
	[MemberData( nameof( AllXmlRecords ) )]
	public async Task EveryXmlRecord_IsLoadedWithItsModel( string xmlId, string model )
	{
		await OdooTestServer.LoginAsync();

		var resolved = await ResolveXmlIdAsync( xmlId );
		Assert.True( resolved is not null, $"{Module}.{xmlId} missing from ir.model.data" );
		Assert.Equal( model, resolved!.Value.Model );

		var count = await OdooRpc.SearchCountAsync( model, OdooDomain.Where( "id", resolved.Value.ResId ) );
		Assert.Equal( 1, count.Value );
	}

	// ---------------------------------------------------------------- hp.settings records

	[OdooFact]
	public async Task Settings_RestrictProperties()
	{
		await OdooTestServer.LoginAsync();
		var row = await ReadAsync( "hp.settings", "hp_settings_restrict_properties", "name", "description", "type", "bool_value" );

		Assert.Equal( "restrict_properties", row[ "name" ] );
		Assert.Equal( "don't allow unknown properties", row[ "description" ] );
		Assert.Equal( "bool", row[ "type" ] );
		Assert.Equal( true, row[ "bool_value" ] );
	}

	[OdooFact]
	public async Task Settings_RestrictTypes()
	{
		await OdooTestServer.LoginAsync();
		var row = await ReadAsync( "hp.settings", "hp_settings_restrict_types", "name", "description", "type", "bool_value" );

		Assert.Equal( "restrict_types", row[ "name" ] );
		Assert.Equal( "only known file types", row[ "description" ] );
		Assert.Equal( "bool", row[ "type" ] );
		Assert.Equal( true, row[ "bool_value" ] );
	}

	[OdooFact]
	public async Task Settings_ClientVersion()
	{
		await OdooTestServer.LoginAsync();
		var row = await ReadAsync( "hp.settings", "hp_settings_client_version", "name", "description", "type", "char_value" );

		Assert.Equal( "client_version", row[ "name" ] );
		Assert.Equal( "char", row[ "type" ] );
		Assert.Equal( ">=1.1.0, <2.0.0", row[ "char_value" ] );
		// this client satisfies the seeded server version range
		Assert.True( HackPDM.Core.Pdm.ClientUpdate.CheckVersion( HackPDM.Core.Pdm.ClientUpdate.Current, row[ "char_value" ] as string ).IsAllowed );
	}

	[OdooFact]
	public async Task Settings_ClientDownload_AreSeededEmpty()
	{
		await OdooTestServer.LoginAsync();
		foreach( string xmlId in new[] { "hp_settings_client_download_url", "hp_settings_client_download_sha256" } )
		{
			var row = await ReadAsync( "hp.settings", xmlId, "name", "type", "char_value" );
			Assert.Equal( xmlId[ "hp_settings_".Length.. ], row[ "name" ] );
			Assert.Equal( "char", row[ "type" ] );
			// empty until an administrator publishes a release
			Assert.True( row[ "char_value" ] is false or "", $"{xmlId} = {row[ "char_value" ]}" );
		}
	}

	// ---------------------------------------------------------------- production hygiene (hp.test isolated in hackpdm_test)

	[OdooFact]
	public async Task ProductionInstall_ExcludesHpTestModel()
	{
		await OdooTestServer.LoginAsync();
		var testModule = await OdooRpc.SearchCountAsync( "ir.module.module",
			OdooDomain.Where( "name", "hackpdm_test" ).And( "state", "installed" ) );
		if( testModule.Value > 0 )
			return;

		var hpTestModel = await OdooRpc.SearchCountAsync( "ir.model", OdooDomain.Where( "model", "hp.test" ) );
		Assert.Equal( 0, hpTestModel.Value );
	}
}
