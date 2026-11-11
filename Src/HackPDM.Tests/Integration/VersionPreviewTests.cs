
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
		var ids = await OdooRpc.SearchAsync( "hp.version", OdooDomain.All );
		Assert.True( ids.Value is { Count: > 0 }, $"no seeded versions: {ids.Error}" );
		int id = ids.Value![ 0 ];

		var version = new HpVersion { preview_image = png };
		var values = version.ComputeValues( false, includedFieldNames: [ "preview_image" ] );
		var written = await OdooRpc.WriteAsync( "hp.version", [ id ], values );
		Assert.True( written.Value, written.Error );

		var read = await OdooRpc.ReadAsync( "hp.version", [ id ], [ "preview_image" ] );
		Assert.True( read.Value is [ _, .. ], read.Error );
		Assert.Equal( png, FileOperations.ConvertFromBase64( read.Value![ 0 ].Text( "preview_image" ) ) );

		var loaded = ( await HpVersion.GetRecordsByIdsAsync( [ id ], includedFields: [ "preview_image" ] ) )?.FirstOrDefault();
		Assert.Equal( png, loaded?.preview_image );
	}
}
