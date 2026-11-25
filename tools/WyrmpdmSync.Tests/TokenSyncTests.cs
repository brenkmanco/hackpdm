using HackPDM.Core.Theming;

using WyrmpdmSync.Penpot;
using WyrmpdmSync.Sync;

using Xunit;

namespace WyrmpdmSync.Tests;

public class TokenSyncTests
{
	[Fact]
	public void Source_ReadsEveryPresetColorAndShapeValue_MatchingTheCompiledPresets()
	{
		ThemePresetsSource code = Fakes.Code();
		Assert.Equal( ThemePresets.Palettes.Count * MaterialRoles.Names.Count, code.Colors.Count );
		Assert.Equal( ThemePresets.Forms.Count * ThemePresetsSource.FormProperties.Count, code.Forms.Count );
		foreach( AppPalette p in ThemePresets.Palettes )
			foreach( string role in MaterialRoles.Names )
				Assert.Equal( Hex.FromArgb( p.Scheme[ role ] ), code.Color( p.Name, role ) );
		Assert.Equal( "16", code.FormValue( FormName.Default, nameof( AppForm.PanelCornerRadius ) ) );
		Assert.Equal( "2", code.FormValue( FormName.Fancy, nameof( AppForm.PanelBorderThickness ) ) );
	}

	[Fact]
	public void Source_EditsOnlyTheLiterals()
	{
		ThemePresetsSource code = Fakes.Code();
		string before = code.Text;
		code.Apply( [ (code.Colors[ (PaletteName.Light, "Primary") ], "0xFF00796B"), (code.Forms[ (FormName.Default, "PanelCornerRadius") ], "12") ] );

		Assert.Equal( "#00796B", code.Color( PaletteName.Light, "Primary" ) );
		Assert.Equal( "12", code.FormValue( FormName.Default, "PanelCornerRadius" ) );
		Assert.Equal( "#B0C6FF", code.Color( PaletteName.Dark, "Primary" ) );
		// everything but the two values is untouched, comments included
		Assert.Equal( before.Length, code.Text.Length );
		Assert.Contains( "// primary", code.Text );
	}

	[Fact]
	public void Link_FindsTheColorTokens_AndCreatesShapeLinksWithoutTokens()
	{
		ThemePresetsSource code = Fakes.Code();
		PenpotFile file = Fakes.File( code );
		SyncManifest manifest = new();
		Assert.Equal( 90 + 16, TokenSync.Link( manifest, file, code ) );
		Assert.All( manifest.Colors, c => Assert.NotNull( c.TokenId ) );
		Assert.All( manifest.Shapes, s => Assert.Null( s.TokenId ) );

		Dictionary<SyncState, int> states = TokenSync.Compare( manifest, file, code ).GroupBy( r => r.State ).ToDictionary( g => g.Key, g => g.Count() );
		Assert.Equal( 90, states[ SyncState.InSync ] );
		Assert.Equal( 16, states[ SyncState.MissingInPenpot ] );
		Assert.Equal( 0, TokenSync.Link( manifest, file, code ) );
	}

	[Fact]
	public void Link_IgnoresCase_AndKeepsPenpotsSpelling()
	{
		// the design file names the Navy blue tokens "Navyblue.*"; the code's palette is NavyBlue
		ThemePresetsSource code = Fakes.Code();
		PenpotFile file = Fakes.File( code, spell: n => n.Replace( "NavyBlue.", "Navyblue." ) );
		SyncManifest manifest = Fakes.Linked( file, code );
		ColorLink navy = manifest.Colors.Single( c => c.Palette == "NavyBlue" && c.Role == "Primary" );
		Assert.Equal( "Navyblue.Primary", navy.Token );
		Assert.Equal( "HackPDM colors/Navy blue", navy.SetName );
		Assert.All( TokenSync.Compare( manifest, file, code ).Where( r => r.Link is ColorLink ), r => Assert.Equal( SyncState.InSync, r.State ) );
	}

	[Fact]
	public void Compare_ShowsWhichSideChanged_AndPullPushTakeTheRightOnes()
	{
		ThemePresetsSource code = Fakes.Code();
		SyncManifest manifest = Fakes.Linked( Fakes.File( code ), code );
		// Penpot moved Light.Primary; the code moved Dark.Error away from the old #123456 Penpot still has;
		// Light.Outline was #ABCDEF last time and both sides have since moved it
		manifest.Colors.Single( c => c.Token == "Dark.Error" ).Synced = "#123456";
		manifest.Colors.Single( c => c.Token == "Light.Outline" ).Synced = "#ABCDEF";
		PenpotFile edited = Fakes.File( code, new() { [ "Light.Primary" ] = "#00796b", [ "Dark.Error" ] = "#123456", [ "Light.Outline" ] = "#000000" } );
		List<TokenReport> reports = TokenSync.Compare( manifest, edited, code );
		Dictionary<string, SyncState> states = reports.ToDictionary( r => r.Link.CodeName, r => r.State );

		Assert.Equal( SyncState.ChangedInPenpot, states[ "Light.Primary" ] );
		Assert.Equal( SyncState.ChangedInCode, states[ "Dark.Error" ] );
		Assert.Equal( SyncState.Conflict, states[ "Light.Outline" ] );

		Assert.Equal( [ "Light.Primary" ], TokenSync.ToPull( reports, Prefer.None ).Select( r => r.Link.CodeName ) );
		Assert.Contains( "Light.Outline", TokenSync.ToPull( reports, Prefer.Penpot ).Select( r => r.Link.CodeName ) );
		List<string> push = [ .. TokenSync.ToPush( reports, Prefer.None ).Select( r => r.Link.CodeName ) ];
		Assert.Contains( "Dark.Error", push );
		Assert.DoesNotContain( "Light.Outline", push );
		Assert.Equal( 16, push.Count( n => n.Contains( "Radius" ) || n.Contains( "Thickness" ) ) );
	}

	[Fact]
	public void Manifest_SavesOnlyItsOwnFields_AndRoundTrips()
	{
		ThemePresetsSource code = Fakes.Code();
		SyncManifest manifest = Fakes.Linked( Fakes.File( code ), code );
		string path = Path.Combine( Path.GetTempPath(), $"penpot-sync-{Guid.NewGuid():N}.json" );
		try
		{
			manifest.Save( path );
			System.Text.Json.Nodes.JsonObject json = System.Text.Json.Nodes.JsonNode.Parse( System.IO.File.ReadAllText( path ) )!.AsObject();
			Assert.Equal( [ "version", "penpot", "colors", "shapes" ], json.Select( kv => kv.Key ) );
			Assert.Equal( [ "token", "palette", "role", "tokenId", "setId", "synced" ], json[ "colors" ]![ 0 ]!.AsObject().Select( kv => kv.Key ) );
			Assert.Equal( [ "token", "form", "property" ], json[ "shapes" ]![ 0 ]!.AsObject().Select( kv => kv.Key ) );

			SyncManifest read = SyncManifest.Load( path );
			Assert.Equal( manifest.Colors.Select( c => (c.Token, c.TokenId, c.Synced) ), read.Colors.Select( c => (c.Token, c.TokenId, c.Synced) ) );
			Assert.Equal( 16, read.Shapes.Count );
		}
		finally
		{
			System.IO.File.Delete( path );
		}
	}

	[Fact]
	public void ApplyToCode_WritesPulledValuesIntoTheSource()
	{
		ThemePresetsSource code = Fakes.Code();
		SyncManifest manifest = Fakes.Linked( Fakes.File( code ), code );
		PenpotFile edited = Fakes.File( code, new() { [ "Light.Primary" ] = "#00796b" } );
		TokenSync.ApplyToCode( code, TokenSync.ToPull( TokenSync.Compare( manifest, edited, code ), Prefer.None ) );
		Assert.Contains( "0xFF00796B", code.Text );
		Assert.Equal( "#00796B", code.Color( PaletteName.Light, "Primary" ) );
	}
}
