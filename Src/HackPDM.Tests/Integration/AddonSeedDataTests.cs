using System.Xml.Linq;

using HackPDM.Infrastructure.Odoo;

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

	/// <summary>Many2one values come back as [id, display_name]; false when empty.</summary>
	private static int? Many2OneId( object? value ) => OdooValues.Many2OneId( value );

	private static int[] Ids( object? value ) => [ .. OdooValues.Ids( value ).Order() ];

	private static void AssertDouble( double expected, object? actual )
		=> Assert.Equal( expected, Convert.ToDouble( actual ), precision: 6 );

	// ---------------------------------------------------------------- coverage of the whole file

	public static TheoryData<string, string> AllXmlRecords()
	{
		var data = new TheoryData<string, string>();
		foreach( var (id, model) in XmlRecords() )
			data.Add( id, model );
		return data;
	}

	[Fact]
	public void DataFile_DeclaresRecords()
	{
		var records = XmlRecords();
		Assert.NotEmpty( records );
		Assert.Equal( records.Count, records.Select( r => r.Id ).Distinct().Count() );
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
		Assert.Equal( "hackpdm client version", row[ "description" ] );
		Assert.Equal( "char", row[ "type" ] );
		Assert.Equal( "1.0.0.27", row[ "char_value" ] );
	}

	// ---------------------------------------------------------------- hp.test records

	[OdooFact]
	public async Task TestRecordOne_AllScalarFieldTypes()
	{
		await OdooTestServer.LoginAsync();
		var row = await ReadAsync( "hp.test", "hp_test_data_one",
			"boolean", "character", "dates", "datetimes", "floats", "html", "integer",
			"json", "monetary", "currency_id", "selection", "text", "manytoone" );

		Assert.Equal( true, row[ "boolean" ] );
		Assert.Equal( "Test Character Field", row[ "character" ] );
		Assert.Equal( "2024-01-01", row[ "dates" ] );
		Assert.Equal( "2024-01-01 12:34:56", row[ "datetimes" ] );
		AssertDouble( 123.456, row[ "floats" ] );
		Assert.Contains( "<strong>Test HTML</strong> content for hp.test.", row[ "html" ] as string );
		Assert.Equal( 42, row[ "integer" ] );

		var json = Assert.IsType<Dictionary<string, object?>>( row[ "json" ] );
		Assert.Equal( "value", json[ "key" ] );
		Assert.Equal( 123, json[ "number" ] );
		Assert.Equal( true, json[ "flag" ] );

		AssertDouble( 19.99, row[ "monetary" ] );
		Assert.NotNull( Many2OneId( row[ "currency_id" ] ) ); // required, defaults to the company currency
		Assert.Equal( "option_a", row[ "selection" ] );
		Assert.Equal( "This is a long text field used for testing.", row[ "text" ] );
		Assert.Null( Many2OneId( row[ "manytoone" ] ) ); // root of the hp.test tree
	}

	[OdooFact]
	public async Task TestRecordTwo_RelationalFields()
	{
		await OdooTestServer.LoginAsync();
		int one = await IdOf( "hp_test_data_one" );
		int two = await IdOf( "hp_test_data_two" );
		var row = await ReadAsync( "hp.test", "hp_test_data_two",
			"character", "monetary", "manytoone", "onetomany", "manytomany", "parent_path" );

		Assert.Equal( "Record Two", row[ "character" ] );
		AssertDouble( 21.99, row[ "monetary" ] );
		Assert.Equal( one, Many2OneId( row[ "manytoone" ] ) );
		Assert.Equal( [ one ], Ids( row[ "manytomany" ] ) );
		Assert.Single( Ids( row[ "onetomany" ] ) );
		Assert.Equal( $"{one}/{two}/", row[ "parent_path" ] );
	}

	[OdooFact]
	public async Task TestRecordTwo_InlineChild_IsCreatedUnderRecordTwo()
	{
		await OdooTestServer.LoginAsync();
		int one = await IdOf( "hp_test_data_one" );
		int two = await IdOf( "hp_test_data_two" );

		var parent = await ReadAsync( "hp.test", "hp_test_data_two", "onetomany" );
		int childId = Assert.Single( Ids( parent[ "onetomany" ] ) );

		var rows = await OdooRpc.ReadAsync( "hp.test", [ childId ], [ "character", "manytoone", "parent_path" ] );
		OdooRow child = Assert.Single( rows.Value! );

		Assert.Equal( "Child 1", child[ "character" ] );
		// The XML sets manytoone=ref('hp_test_data_one') on the child, but a (0, 0, {...}) command
		// inside record two's one2many always makes record two the parent.
		Assert.Equal( two, Many2OneId( child[ "manytoone" ] ) );
		Assert.Equal( $"{one}/{two}/{childId}/", child[ "parent_path" ] );
	}

	[OdooFact]
	public async Task TestRecordOne_ChildrenAreRecordTwo()
	{
		await OdooTestServer.LoginAsync();
		int two = await IdOf( "hp_test_data_two" );
		var row = await ReadAsync( "hp.test", "hp_test_data_one", "onetomany", "manytomany" );

		Assert.Equal( [ two ], Ids( row[ "onetomany" ] ) );
		Assert.Empty( Ids( row[ "manytomany" ] ) ); // many2many is not symmetric
	}
}
