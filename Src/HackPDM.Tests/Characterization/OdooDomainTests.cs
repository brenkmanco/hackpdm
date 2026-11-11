using System.Linq;

using HackPDM.Infrastructure.Odoo;
using HackPDM.Shared.GlobalData;

using Xunit;

namespace HackPDM.Tests.Characterization;

/// <summary>What <see cref="OdooDomain"/> sends: Odoo's prefix-notation lists of [field, op, value] terms.</summary>
public class OdooDomainTests
{
	private static object?[] Wire( OdooDomain domain ) => [ .. domain.ToRpc() ];

	[Fact]
	public void All_IsEmpty() => Assert.Empty( OdooDomain.All.ToRpc() );

	[Fact]
	public void Terms_AreAndedImplicitly()
		=> Assert.Equal(
			[ new object?[] { "name", "=", "a" }, new object?[] { "deleted", "=", false } ],
			Wire( OdooDomain.Where( "name", "a" ).And( "deleted", false ) ) );

	[Fact]
	public void EnumOperator_IsSpelledAsOdooDoes()
		=> Assert.Equal( [ new object?[] { "name", "not ilike", "x" } ], Wire( OdooDomain.Where( "name", Operators.NotILike, "x" ) ) );

	[Fact]
	public void IdLists_AreSentAsLists()
	{
		object?[] term = (object?[])Wire( OdooDomain.IdIn( [ 1, 2 ] ) )[ 0 ]!;
		Assert.Equal( [ 1, 2 ], ( (System.Collections.IEnumerable)term[ 2 ]! ).Cast<object>() );
	}

	[Fact]
	public void Or_OfTwoMultiTermDomains_AddsTheAnds()
	{
		OdooDomain a = OdooDomain.Where( "a", 1 ).And( "b", 2 );
		OdooDomain c = OdooDomain.Where( "c", 3 );

		Assert.Equal(
			[ "|", "&", new object?[] { "a", "=", 1 }, new object?[] { "b", "=", 2 }, new object?[] { "c", "=", 3 } ],
			Wire( a | c ) );
	}

	[Fact]
	public void And_WithEmpty_IsTheOther()
	{
		OdooDomain a = OdooDomain.Where( "a", 1 );
		Assert.Same( a, a & OdooDomain.All );
		Assert.Same( a, OdooDomain.All & a );
	}

	[Fact]
	public void Not_WrapsOneExpression()
		=> Assert.Equal( [ "!", "&", new object?[] { "a", "=", 1 }, new object?[] { "b", "=", 2 } ],
			Wire( !OdooDomain.Where( "a", 1 ).And( "b", 2 ) ) );
}
