
using HackPDM.Tests.Characterization;

namespace HackPDM.Tests.Integration;

/// <summary>
/// Committing a file into a folder the server doesn't have yet creates the missing directories.
/// HpDirectory.CreateNew used to index its result array wrongly, so creating any directory threw.
/// Requires the data from Odoo/ci/seed_smoke_data.py (root / Projects / Parts).
/// </summary>
[Collection( OdooCollection.Name )]
public class DirectoryCreationTests
{
	public DirectoryCreationTests() => OdooTestServer.ConfigureClient();

	[OdooFact]
	public async Task CreateNew_CreatesEachMissingLevel_UnderTheLastExistingOne()
	{
		await OdooTestServer.LoginAsync();
		string a = $"new-{Guid.NewGuid():N}", b = "deeper";
		List<string> paths = [ "Projects", "Parts", a, b ];

		HpDirectory[]? created = await HpDirectory.CreateNew( paths );

		Assert.Equal( [ "Projects", "Parts", a, b ], paths ); // the caller's list is untouched
		Assert.NotNull( created );
		Assert.Equal( [ a, b ], created!.Select( d => d.name ) );
		var first = await HpDirectory.GetRecordByIdAsync( created[ 0 ].id!.Value );
		var second = await HpDirectory.GetRecordByIdAsync( created[ 1 ].id!.Value );
		Assert.Equal( "root / Projects / Parts", first?.parent_id?.name ); // the display name is the full path
		Assert.Equal( created[ 0 ].id, second?.parent_id?.id );

		// asking again finds the whole chain and creates nothing
		HpDirectory[]? again = await HpDirectory.CreateNew( paths );
		Assert.Equal( created[ 1 ].id, Assert.Single( again! ).id );
	}
}
