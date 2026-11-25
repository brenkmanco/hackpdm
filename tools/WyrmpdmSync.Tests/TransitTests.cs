using System.Text.Json.Nodes;

using WyrmpdmSync.Penpot;

using Xunit;

namespace WyrmpdmSync.Tests;

public class TransitTests
{
	[Fact]
	public void Decode_MapsKeywordsUuidsAndCacheReferences()
	{
		// cached in order of first use: ^0 "~:name", ^1 "~:type", ^2 "~:color", ^3 "~:id", ^4 "~:items" (uuids are not cached)
		const string text = """
			["^ ","~:name","Light.Primary","~:type","~:color","~:id","~u394343ae-7425-8033-8009-002a2c24268c",
			 "~:items",[["^ ","^0","a","^1","~:color"],["^ ","^0","b","^1","^2"]]]
			""";
		JsonObject map = Assert.IsType<JsonObject>( Transit.Decode( text ) );
		Assert.Equal( "Light.Primary", (string?)map[ "name" ] );
		Assert.Equal( "color", (string?)map[ "type" ] );
		Assert.Equal( "394343ae-7425-8033-8009-002a2c24268c", (string?)map[ "id" ] );
		JsonArray items = Assert.IsType<JsonArray>( map[ "items" ] );
		Assert.Equal( "a", (string?)items[ 0 ]![ "name" ] );
		Assert.Equal( "color", (string?)items[ 1 ]![ "type" ] );
	}

	[Fact]
	public void Decode_ResolvingACacheReference_DoesNotCacheItAgain()
	{
		// ^0 = "~:name", ^1 = "~:kind". Using ^0 must not add a new entry, so ^1 still means "~:kind".
		const string text = """[["^ ","~:name","x","~:kind","y"],["^ ","^0","z","^1","w"]]""";
		JsonArray list = Assert.IsType<JsonArray>( Transit.Decode( text ) );
		Assert.Equal( "w", (string?)list[ 1 ]![ "kind" ] );
	}

	[Fact]
	public void Decode_TaggedRecordsAndOrderedMaps()
	{
		// cache: ^0 "~#penpot/token-set", ^1 "~:name", ^2 "~:tokens", ^3 "~#ordered-map", ^4 "~#penpot/token"
		const string text = """
			["~#penpot/token-set",["^ ","~:name","HackPDM colors/Light",
			  "~:tokens",["~#ordered-map",[["Light.Primary",["~#penpot/token",["^ ","^1","Light.Primary","~:value","#485E92"]]]]]]]
			""";
		JsonObject set = Assert.IsType<JsonObject>( Transit.Decode( text ) );
		Assert.Equal( "penpot/token-set", (string?)set[ "~tag" ] );
		JsonObject token = Assert.IsType<JsonObject>( set[ "tokens" ]![ "Light.Primary" ] );
		Assert.Equal( "#485E92", (string?)token[ "value" ] );
		Assert.Equal( "Light.Primary", (string?)token[ "name" ] );
	}

	[Fact]
	public void Write_MarksKeywordsUuidsAndSets_AndReadsBack()
	{
		JsonObject change = new()
		{
			[ "type" ] = "mod-obj", [ "pageId" ] = "394343ae-7425-8033-8009-002baedf0d3e",
			[ "operations" ] = new JsonArray( new JsonObject
			{
				[ "type" ] = "set", [ "attr" ] = "strokes",
				[ "val" ] = new JsonArray( new JsonObject { [ "stroke-color" ] = "#735573", [ "stroke-style" ] = "solid", [ "stroke-width" ] = 2.5 } ),
			} ),
			[ "setIds" ] = new JsonArray( "394343ae-7425-8033-8009-002a2bd950b6" ),
			[ "name" ] = "~odd",
		};
		string text = TransitWriter.Encode( change );

		Assert.Contains( "\"~:type\",\"~:mod-obj\"", text );
		Assert.Contains( "\"~:page-id\",\"~u394343ae-7425-8033-8009-002baedf0d3e\"", text );
		Assert.Contains( "\"~:stroke-style\",\"~:solid\"", text );
		Assert.Contains( "\"~:stroke-color\",\"#735573\"", text );
		Assert.Contains( "\"~:set-ids\",[\"~#set\",", text );
		Assert.Contains( "\"~~odd\"", text );

		// and the reader understands it
		JsonObject back = Assert.IsType<JsonObject>( Transit.Decode( text ) );
		Assert.Equal( "solid", (string?)back[ "operations" ]![ 0 ]![ "val" ]![ 0 ]![ "stroke-style" ] );
		Assert.Equal( "~odd", (string?)back[ "name" ] );
	}

	[Fact]
	public void Decode_ScalarsAndEscapes()
	{
		JsonArray list = Assert.IsType<JsonArray>( Transit.Decode( """["~?t","~_","~i42","~~tilde","~m0",1.5]""" ) );
		Assert.True( (bool)list[ 0 ]! );
		Assert.Null( list[ 1 ] );
		Assert.Equal( 42, (long)list[ 2 ]! );
		Assert.Equal( "~tilde", (string?)list[ 3 ] );
		Assert.StartsWith( "1970-01-01", (string?)list[ 4 ] );
		Assert.Equal( 1.5, (double)list[ 5 ]! );
	}
}
