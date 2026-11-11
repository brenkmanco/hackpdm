using System.Collections;

using HackPDM.Core;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Infrastructure.SldWrks;
using HackPDM.Tests.Core;

namespace HackPDM.Tests.Integration;

/// <summary>
/// A SolidWorks preview read from the file survives the trip to Odoo's preview_image field
/// (sent the same way a staged version sends it) and comes back unchanged.
/// </summary>
[Collection( OdooCollection.Name )]
public class VersionPreviewTests
{
	public VersionPreviewTests() => OdooTestServer.ConfigureClient();

	[OdooFact]
	public async Task SolidWorksPreview_IsStoredAndReadBack_Unchanged()
	{
		await OdooTestServer.LoginAsync();
		byte[] png = new SwFileReader().GetPreviewPng( SwSamples.Old( "old.SLDPRT" ) )!;
		ArrayList ids = await OdooClient.SearchAsync( "hp.version", [ new ArrayList() ] );
		Assert.True( ids.Count > 0, $"no seeded versions: {OdooClient.LatestException}" );
		int id = Convert.ToInt32( ids[ 0 ] );

		var version = new HpVersion { preview_image = png };
		Hashtable values = version.ComputeHashtable( false, includedFieldNames: [ "preview_image" ] );
		Assert.True( await OdooClient.UpdateAsync( "hp.version", id, values ), OdooClient.LatestException );

		var read = await OdooClient.ReadAsync( "hp.version", [ id ], [ "preview_image" ] );
		Assert.True( read is [ Hashtable row, .. ], OdooClient.LatestException );
		Assert.Equal( png, FileOperations.ConvertFromBase64( ( (Hashtable)read[ 0 ]! )[ "preview_image" ] as string ) );

		var loaded = ( await HpVersion.GetRecordsByIdsAsync( [ id ], includedFields: [ "preview_image" ] ) )?.FirstOrDefault();
		Assert.Equal( png, loaded?.preview_image );
	}
}
