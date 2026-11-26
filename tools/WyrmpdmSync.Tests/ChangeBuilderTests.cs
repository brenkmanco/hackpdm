using System.Text.Json.Nodes;

using WyrmpdmSync.Penpot;
using WyrmpdmSync.Sync;

using Xunit;

namespace WyrmpdmSync.Tests;

public class ChangeBuilderTests
{
	private static ChangeSet Push( ThemePresetsSource code, PenpotFile file, SyncManifest manifest, Func<TokenReport, bool> which )
		=> ChangeBuilder.ForTokens( file, [ .. TokenSync.ToPush( TokenSync.Compare( manifest, file, code ), Prefer.None ).Where( which ) ] );

	[Fact]
	public void ChangedColor_UpdatesTheToken_TheLibraryColor_AndBoundShapes()
	{
		ThemePresetsSource code = Fakes.Code();
		Guid colorId = Guid.NewGuid();
		JsonObject library = new()
		{
			[ colorId.ToString() ] = new JsonObject { [ "id" ] = colorId.ToString(), [ "name" ] = "Primary", [ "path" ] = "HackPDM colors / Light", [ "color" ] = "#485e92", [ "opacity" ] = 1 },
		};
		JsonObject objects = new()
		{
			[ "aaaaaaaa-0000-0000-0000-000000000001" ] = new JsonObject
			{
				[ "type" ] = "rect", [ "fills" ] = new JsonArray( new JsonObject { [ "fill-color" ] = "#485e92", [ "fill-opacity" ] = 1 } ),
				[ "applied-tokens" ] = new JsonObject { [ "fill" ] = "Light.Primary" },
			},
			[ "aaaaaaaa-0000-0000-0000-000000000002" ] = new JsonObject
			{
				[ "type" ] = "rect", [ "fills" ] = new JsonArray( new JsonObject { [ "fill-color" ] = "#ffffff", [ "fill-opacity" ] = 1 } ),
				[ "applied-tokens" ] = new JsonObject { [ "fill" ] = "Light.OnPrimary" },
			},
		};
		PenpotFile file = Fakes.File( code, objects: objects, libraryColors: library );
		SyncManifest manifest = Fakes.Linked( file, code );
		// the code moved Light.Primary since the last sync, which was the value Penpot still has
		code.Apply( [ (code.Colors[ (HackPDM.Core.Theming.PaletteName.Light, "Primary") ], "0xFF00796B") ] );

		ChangeSet changes = Push( code, file, manifest, r => r.Link is ColorLink );

		JsonObject token = changes.Changes.Single( c => (string?)c[ "type" ] == "set-token" );
		Assert.Equal( Fakes.Id( "Light.Primary" ).ToString(), (string?)token[ "tokenId" ] );
		Assert.Equal( "#00796B", (string?)token[ "attrs" ]![ "value" ] );
		Assert.Equal( "color", (string?)token[ "attrs" ]![ "type" ] );

		JsonObject color = changes.Changes.Single( c => (string?)c[ "type" ] == "mod-color" );
		Assert.Equal( "#00796b", (string?)color[ "color" ]![ "color" ] );

		JsonObject shape = changes.Changes.Single( c => (string?)c[ "type" ] == "mod-obj" );
		Assert.Equal( "aaaaaaaa-0000-0000-0000-000000000001", (string?)shape[ "id" ] );
		Assert.Equal( "#00796b", (string?)shape[ "operations" ]![ 0 ]![ "val" ]![ 0 ]![ "fill-color" ] );
		Assert.DoesNotContain( changes.Changes, c => (string?)c[ "type" ] == "set-token-set" );
	}

	[Fact]
	public void MissingShapeTokens_CreateTheirSets_AndSwitchThemOn()
	{
		ThemePresetsSource code = Fakes.Code();
		PenpotFile file = Fakes.File( code );
		SyncManifest manifest = Fakes.Linked( file, code );

		ChangeSet changes = Push( code, file, manifest, r => r.Link is ShapeLink );

		Assert.Equal( 4, changes.Changes.Count( c => (string?)c[ "type" ] == "set-token-set" ) );
		Assert.Equal( 16, changes.Changes.Count( c => (string?)c[ "type" ] == "set-token" ) );
		JsonObject radius = changes.Changes.Single( c => (string?)c[ "attrs" ]?[ "name" ] == "Default.PanelCornerRadius" );
		Assert.Equal( "border-radius", (string?)radius[ "attrs" ]![ "type" ] );
		Assert.Equal( "16", (string?)radius[ "attrs" ]![ "value" ] );
		Assert.Equal( "stroke-width", (string?)changes.Changes.Single( c => (string?)c[ "attrs" ]?[ "name" ] == "Fancy.PanelBorderThickness" )[ "attrs" ]![ "type" ] );

		// the set is created before its tokens, and the status change keeps the already-active set
		int firstSet = changes.Changes.FindIndex( c => (string?)c[ "type" ] == "set-token-set" );
		int firstToken = changes.Changes.FindIndex( c => (string?)c[ "type" ] == "set-token" );
		Assert.True( firstSet < firstToken );
		JsonArray active = Assert.IsType<JsonArray>( changes.Changes.Last()[ "setIds" ] );
		Assert.Equal( 5, active.Count );
		Assert.Contains( Fakes.Id( "set Light" ).ToString(), active.Select( n => (string?)n ) );
		Assert.Equal( 16, changes.Ids.Count );
	}

	[Fact]
	public void NothingChanged_MeansNoChanges()
	{
		ThemePresetsSource code = Fakes.Code();
		PenpotFile file = Fakes.File( code );
		SyncManifest manifest = Fakes.Linked( file, code );
		Assert.True( Push( code, file, manifest, r => r.Link is ColorLink ).IsEmpty );
	}

	[Fact]
	public void ChangedColor_RepaintsBoundTextShapes_ContentAndPositionData()
	{
		ThemePresetsSource code = Fakes.Code();
		string initialHex = code.Color( HackPDM.Core.Theming.PaletteName.Light, "OnSurface" )!.ToLowerInvariant();
		const string textId = "aaaaaaaa-0000-0000-0000-000000000010";
		JsonObject objects = new()
		{
			[ textId ] = new JsonObject
			{
				[ "id" ] = textId, [ "type" ] = "text",
				[ "applied-tokens" ] = new JsonObject { [ "fill" ] = "Light.OnSurface" },
				[ "content" ] = new JsonObject
				{
					[ "type" ] = "root",
					[ "children" ] = new JsonArray( new JsonObject
					{
						[ "type" ] = "paragraph-set",
						[ "children" ] = new JsonArray( new JsonObject
						{
							[ "type" ] = "paragraph",
							[ "children" ] = new JsonArray( new JsonObject
							{
								[ "text" ] = "Hello",
								[ "fills" ] = new JsonArray( new JsonObject { [ "fill-color" ] = initialHex, [ "fill-opacity" ] = 1 } ),
							} ),
						} ),
					} ),
				},
				[ "position-data" ] = new JsonArray( new JsonObject
				{
					[ "~tag" ] = "position-data", [ "x" ] = 12.5, [ "y" ] = 24.0, [ "text" ] = "Hello",
					[ "fills" ] = new JsonArray( new JsonObject { [ "fill-color" ] = initialHex, [ "fill-opacity" ] = 1 } ),
				} ),
			},
		};
		PenpotFile file = Fakes.File( code, objects: objects );
		SyncManifest manifest = Fakes.Linked( file, code );
		code.Apply( [ (code.Colors[ (HackPDM.Core.Theming.PaletteName.Light, "OnSurface") ], "0xFF112233") ] );

		ChangeSet changes = Push( code, file, manifest, r => r.Link is ColorLink );

		JsonObject shape = changes.Changes.Single( c => (string?)c[ "type" ] == "mod-obj" );
		Assert.Equal( textId, (string?)shape[ "id" ] );
		JsonArray ops = shape[ "operations" ]!.AsArray();
		Assert.Equal( 2, ops.Count );

		JsonObject contentOp = ops.Single( o => (string?)o![ "attr" ] == "content" )!.AsObject();
		JsonObject leaf = contentOp[ "val" ]![ "children" ]![ 0 ]![ "children" ]![ 0 ]![ "children" ]![ 0 ]!.AsObject();
		Assert.Equal( "Hello", (string?)leaf[ "text" ] );
		Assert.Equal( "#112233", (string?)leaf[ "fills" ]![ 0 ]![ "fill-color" ] );
		Assert.Equal( 1.0, (double)leaf[ "fills" ]![ 0 ]![ "fill-opacity" ]! );

		JsonObject posOp = ops.Single( o => (string?)o![ "attr" ] == "position-data" )!.AsObject();
		JsonObject glyph = posOp[ "val" ]![ 0 ]!.AsObject();
		Assert.Equal( "position-data", (string?)glyph[ "~tag" ] );
		Assert.Equal( 12.5, (double)glyph[ "x" ]! );
		Assert.Equal( 24.0, (double)glyph[ "y" ]! );
		Assert.Equal( "Hello", (string?)glyph[ "text" ] );
		Assert.Equal( "#112233", (string?)glyph[ "fills" ]![ 0 ]![ "fill-color" ] );
		Assert.Equal( 1.0, (double)glyph[ "fills" ]![ 0 ]![ "fill-opacity" ]! );
	}

	[Fact]
	public void OpacityOnlyColorChange_UpdatesLibraryColorAndBoundShapes()
	{
		ThemePresetsSource code = Fakes.Code();
		Guid colorId = Guid.NewGuid();
		JsonObject library = new()
		{
			[ colorId.ToString() ] = new JsonObject { [ "id" ] = colorId.ToString(), [ "name" ] = "Primary", [ "path" ] = "HackPDM colors / Light", [ "color" ] = "#485e92", [ "opacity" ] = 1 },
		};
		const string shapeId = "aaaaaaaa-0000-0000-0000-000000000020";
		JsonObject objects = new()
		{
			[ shapeId ] = new JsonObject
			{
				[ "type" ] = "rect",
				[ "fills" ] = new JsonArray( new JsonObject { [ "fill-color" ] = "#485e92", [ "fill-opacity" ] = 1 } ),
				[ "applied-tokens" ] = new JsonObject { [ "fill" ] = "Light.Primary" },
			},
		};
		PenpotFile file = Fakes.File( code, objects: objects, libraryColors: library );
		SyncManifest manifest = Fakes.Linked( file, code );
		// same RGB (#485E92), alpha changed from 0xFF to 0x80 (#485E9280)
		code.Apply( [ (code.Colors[ (HackPDM.Core.Theming.PaletteName.Light, "Primary") ], "0x80485E92") ] );

		ChangeSet changes = Push( code, file, manifest, r => r.Link is ColorLink );

		JsonObject token = changes.Changes.Single( c => (string?)c[ "type" ] == "set-token" );
		Assert.Equal( "#485E9280", (string?)token[ "attrs" ]![ "value" ] );

		JsonObject color = changes.Changes.Single( c => (string?)c[ "type" ] == "mod-color" );
		Assert.Equal( "#485e92", (string?)color[ "color" ]![ "color" ] );
		Assert.Equal( 0.502, (double)color[ "color" ]![ "opacity" ]! );

		JsonObject shape = changes.Changes.Single( c => (string?)c[ "type" ] == "mod-obj" );
		Assert.Equal( shapeId, (string?)shape[ "id" ] );
		Assert.Equal( "#485e92", (string?)shape[ "operations" ]![ 0 ]![ "val" ]![ 0 ]![ "fill-color" ] );
		Assert.Equal( 0.502, (double)shape[ "operations" ]![ 0 ]![ "val" ]![ 0 ]![ "fill-opacity" ]! );
	}

	[Fact]
	public void NewTokenSets_PreserveActiveThemeIds()
	{
		ThemePresetsSource code = Fakes.Code();
		Guid themeA = Guid.Parse( "bbbbbbbb-0000-0000-0000-000000000001" );
		Guid themeB = Guid.Parse( "bbbbbbbb-0000-0000-0000-000000000002" );
		JsonObject raw = Fakes.File( code ).Raw;
		raw[ "data" ]![ "tokens-status" ]![ "active-theme-ids" ] = new JsonArray( themeA.ToString(), themeB.ToString() );
		PenpotFile file = PenpotFile.Parse( raw );
		SyncManifest manifest = Fakes.Linked( file, code );

		ChangeSet changes = Push( code, file, manifest, r => r.Link is ShapeLink );

		JsonObject status = changes.Changes.Single( c => (string?)c[ "type" ] == "set-tokens-status" );
		JsonArray themes = Assert.IsType<JsonArray>( status[ "themeIds" ] );
		Assert.Equal( [ themeA.ToString(), themeB.ToString() ], themes.Select( n => (string?)n ) );

		JsonArray sets = Assert.IsType<JsonArray>( status[ "setIds" ] );
		List<string?> setIds = [ .. sets.Select( n => (string?)n ) ];
		Assert.Equal( 5, setIds.Count );
		Assert.Contains( Fakes.Id( "set Light" ).ToString(), setIds );
		foreach( JsonObject created in changes.Changes.Where( c => (string?)c[ "type" ] == "set-token-set" ) )
			Assert.Contains( (string?)created[ "id" ], setIds );
	}

	[Fact]
	public void ChangedColor_RepaintsBoundTextShapes_WhenLeafOrPositionDataFillsMissingOrEmpty()
	{
		ThemePresetsSource code = Fakes.Code();
		const string textId = "aaaaaaaa-0000-0000-0000-000000000030";
		JsonObject objects = new()
		{
			[ textId ] = new JsonObject
			{
				[ "id" ] = textId, [ "type" ] = "text",
				[ "applied-tokens" ] = new JsonObject { [ "fill" ] = "Light.OnSurface" },
				[ "content" ] = new JsonObject
				{
					[ "type" ] = "root",
					[ "children" ] = new JsonArray( new JsonObject
					{
						[ "type" ] = "paragraph-set",
						[ "children" ] = new JsonArray( new JsonObject
						{
							[ "type" ] = "paragraph",
							[ "children" ] = new JsonArray(
								new JsonObject { [ "text" ] = "Missing" },
								new JsonObject { [ "text" ] = "Empty", [ "fills" ] = new JsonArray() } ),
						} ),
					} ),
				},
				[ "position-data" ] = new JsonArray(
					new JsonObject { [ "~tag" ] = "position-data", [ "x" ] = 0.0, [ "y" ] = 10.0, [ "text" ] = "Missing" },
					new JsonObject { [ "~tag" ] = "position-data", [ "x" ] = 40.0, [ "y" ] = 10.0, [ "text" ] = "Empty", [ "fills" ] = new JsonArray() } ),
			},
		};
		PenpotFile file = Fakes.File( code, objects: objects );
		SyncManifest manifest = Fakes.Linked( file, code );
		code.Apply( [ (code.Colors[ (HackPDM.Core.Theming.PaletteName.Light, "OnSurface") ], "0x80112233") ] );

		ChangeSet changes = Push( code, file, manifest, r => r.Link is ColorLink );

		JsonObject shape = changes.Changes.Single( c => (string?)c[ "type" ] == "mod-obj" );
		JsonArray ops = shape[ "operations" ]!.AsArray();
		Assert.Equal( 2, ops.Count );

		JsonObject root = ops.Single( o => (string?)o![ "attr" ] == "content" )![ "val" ]!.AsObject();
		Assert.False( root.ContainsKey( "fills" ) );
		JsonObject paragraphSet = root[ "children" ]![ 0 ]!.AsObject();
		Assert.False( paragraphSet.ContainsKey( "fills" ) );
		JsonObject paragraph = paragraphSet[ "children" ]![ 0 ]!.AsObject();
		Assert.False( paragraph.ContainsKey( "fills" ) );
		JsonArray leaves = paragraph[ "children" ]!.AsArray();
		Assert.Equal( 2, leaves.Count );
		foreach( JsonNode? leaf in leaves )
		{
			JsonObject fill = Assert.Single( leaf![ "fills" ]!.AsArray() )!.AsObject();
			Assert.Equal( "#112233", (string?)fill[ "fill-color" ] );
			Assert.Equal( 0.502, (double)fill[ "fill-opacity" ]! );
		}

		JsonArray posData = ops.Single( o => (string?)o![ "attr" ] == "position-data" )![ "val" ]!.AsArray();
		Assert.Equal( 2, posData.Count );
		foreach( JsonNode? entry in posData )
		{
			JsonObject fill = Assert.Single( entry![ "fills" ]!.AsArray() )!.AsObject();
			Assert.Equal( "#112233", (string?)fill[ "fill-color" ] );
			Assert.Equal( 0.502, (double)fill[ "fill-opacity" ]! );
		}
	}
}
