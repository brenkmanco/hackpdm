using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Mvvm.Models.Odoo;

using Xunit;

namespace HackPDM.Tests.Characterization;

/// <summary>
/// Golden values for the Odoo search domain the search window sends (as it goes over the wire).
/// Property filters need a live server and are covered by the integration tests instead.
/// </summary>
[Collection( SharedStateCollection.Name )]
public class EntrySearchDomainTests
{
	private static EntrySearchQuery Query(
		string fileName = "", string comparer = "ilike", bool mine = false, bool deleted = false, bool localOnly = false )
		=> new( fileName, comparer, mine, deleted, localOnly, MaxResults: 100, Properties: [] );

	private static async Task<object[]> DomainOf( EntrySearchQuery query )
		=> [ .. ( await EntrySearchService.BuildDomainAsync( query ) ).ToRpc() ];

	[Fact]
	public async Task EmptyQuery_MatchesEverything()
		=> Assert.Empty( await DomainOf( Query() ) );

	[Fact]
	public async Task Name_UsesChosenComparer()
		=> Assert.Equal( [ new object[] { "name", "ilike", "brack" } ], await DomainOf( Query( "brack" ) ) );

	[Fact]
	public async Task AllRemoteFilters_InOrder()
	{
		object[] domain = await DomainOf( Query( "bracket.txt", "=", mine: true, deleted: true ) );

		Assert.Equal(
			[
				new object[] { "name", "=", "bracket.txt" },
				new object?[] { "checkout_user", "=", OdooDefaults.Instance.OdooId },
				new object[] { "deleted", "=", true },
			],
			domain );
	}

	[Fact]
	public async Task LocalOnly_IgnoresRemoteFilters()
		=> Assert.Equal( [ new object[] { "name", "ilike", "x" } ], await DomainOf( Query( "x", mine: true, deleted: true, localOnly: true ) ) );

	[Fact]
	public void TreeSegmentsBelowRoot_Remote_SplitsCompleteName()
	{
		var row = new SearchRow { Id = 42, Name = "bracket.txt", Directory = "root / Projects / Parts" };
		Assert.Equal( [ "Projects", "Parts" ], new EntrySearchService().TreeSegmentsBelowRoot( row ) );
	}

	[Fact]
	public void TreeSegmentsBelowRoot_Local_IsRelativeToWorkingFolder()
	{
		using var pwa = new PwaScope();
		var service = new EntrySearchService();
		Assert.Equal( [ "Projects", "Parts" ], service.TreeSegmentsBelowRoot( new SearchRow { Directory = Path.Combine( pwa.Absolute, "Projects", "Parts" ) } ) );
		Assert.Empty( service.TreeSegmentsBelowRoot( new SearchRow { Directory = pwa.Absolute } ) );
	}
}
