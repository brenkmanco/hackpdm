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
}
