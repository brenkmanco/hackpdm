using System.Xml.Linq;

namespace HackPDM.Tests.Operations;

/// <summary>
/// Verifies offline that the hackpdm addon's seed file declares only production <c>hp.settings</c> records.
/// </summary>
public class AddonSeedDataFileTests
{
	private const string DataFile = "Odoo/addons/hackpdm/data/hp_settings_data.xml";

	internal static string FindRepoFile( string relativePath )
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
	internal static List<(string Id, string Model)> XmlRecords()
		=> XDocument.Load( FindRepoFile( DataFile ) )
			.Descendants( "record" )
			.Select( r => ( (string)r.Attribute( "id" )!, (string)r.Attribute( "model" )! ) )
			.ToList();

	[Fact]
	public void DataFile_DeclaresOnlyProductionSettingsRecords()
	{
		var records = XmlRecords();
		Assert.NotEmpty( records );
		Assert.Equal( records.Count, records.Select( r => r.Id ).Distinct().Count() );
		Assert.All( records, r => Assert.Equal( "hp.settings", r.Model ) );
	}
}
