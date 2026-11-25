using System.Text.Json.Nodes;

using HackPDM.Core.Theming;

using WyrmpdmSync.Penpot;
using WyrmpdmSync.Sync;

using Xunit;

namespace WyrmpdmSync.Tests;

public class ColorSyncTests
{
	private static readonly Guid FileId = Guid.Parse( "3db45ae8-1dfd-81ce-8009-0028caf8f15e" );

	/// <summary>A Penpot file holding every preset color as a token, the way the design file does, with overrides.</summary>
	internal static PenpotFile FakeFile( Dictionary<string, string>? overrides = null, Func<string, string>? spell = null, params string[] missing )
	{
		JsonObject sets = [];
		foreach( IGrouping<PaletteName, (PaletteName Palette, string Role, string Hex)> group in CodeColors.All().GroupBy( c => c.Palette ) )
		{
			JsonObject tokens = [];
			foreach( (PaletteName palette, string role, string hex) in group )
			{
				string name = CodeColors.TokenName( palette, role );
				if( missing.Contains( name ) )
					continue;
				string penpotName = spell?.Invoke( name ) ?? name;
				tokens[ penpotName ] = new JsonObject
				{
					[ "id" ] = Guid.NewGuid().ToString(), [ "name" ] = penpotName, [ "type" ] = "color",
					[ "value" ] = overrides?.GetValueOrDefault( name ) ?? hex,
				};
			}
			sets[ $"S-{group.Key}" ] = new JsonObject
			{
				[ "id" ] = Guid.NewGuid().ToString(), [ "name" ] = $"HackPDM colors/{group.Key}", [ "tokens" ] = tokens,
			};
		}
		return PenpotFile.Parse( new JsonObject
		{
			[ "id" ] = FileId.ToString(), [ "name" ] = "HackPDM -- Uno Materials Design", [ "revn" ] = 42, [ "vern" ] = 0,
			[ "features" ] = new JsonArray( "components/v2", "design-tokens/v1" ),
			[ "data" ] = new JsonObject
			{
				[ "pages" ] = new JsonArray( "11111111-1111-1111-1111-111111111111" ),
				[ "pagesIndex" ] = new JsonObject
				{
					[ "11111111-1111-1111-1111-111111111111" ] = new JsonObject { [ "name" ] = "Colors", [ "objects" ] = new JsonObject() },
				},
				// nested a level deep on purpose: the reader finds sets by their shape, not by a fixed path
				[ "tokensLib" ] = new JsonObject { [ "sets" ] = sets },
			},
		} );
	}

	private static SyncManifest LinkedManifest( PenpotFile file )
	{
		SyncManifest manifest = new() { Penpot = { FileId = FileId } };
		ColorSync.Link( manifest, file );
		return manifest;
	}

	[Fact]
	public void Parse_ReadsPagesAndEveryColorToken()
	{
		PenpotFile file = FakeFile();
		Assert.Equal( 42, file.Revn );
		Assert.Equal( "Colors", Assert.Single( file.Pages ).Name );
		Assert.Equal( ThemePresets.Palettes.Count * MaterialRoles.Names.Count, file.Tokens.Count );
		Assert.All( file.Tokens, t => Assert.StartsWith( "HackPDM colors/", t.SetName ) );
	}

	[Fact]
	public void Link_MatchesTokensByName_AndStartsInSync()
	{
		PenpotFile file = FakeFile();
		SyncManifest manifest = new();
		Assert.Equal( 90, ColorSync.Link( manifest, file ) );
		Assert.All( manifest.Colors, c => Assert.NotNull( c.TokenId ) );
		Assert.All( ColorSync.Compare( manifest, file ), r => Assert.Equal( SyncState.InSync, r.State ) );
		Assert.Equal( 0, ColorSync.Link( manifest, file ) );
	}

	[Fact]
	public void Compare_ShowsWhichSideChanged()
	{
		SyncManifest manifest = LinkedManifest( FakeFile() );
		// Penpot moved Light.Primary; the code moved Dark.Error away from the old #123456 Penpot still has;
		// Light.Outline was #ABCDEF last time and both sides have since moved it
		manifest.Colors.Single( c => c.Token == "Dark.Error" ).Synced = "#123456";
		manifest.Colors.Single( c => c.Token == "Light.Outline" ).Synced = "#ABCDEF";
		PenpotFile edited = FakeFile( new() { [ "Light.Primary" ] = "#00796b", [ "Dark.Error" ] = "#123456", [ "Light.Outline" ] = "#000000" } );
		// the edited file has new token ids; the name still finds them
		Dictionary<string, SyncState> states = ColorSync.Compare( manifest, edited ).ToDictionary( r => r.Link.Token, r => r.State );

		Assert.Equal( SyncState.ChangedInPenpot, states[ "Light.Primary" ] );
		Assert.Equal( SyncState.ChangedInCode, states[ "Dark.Error" ] );
		Assert.Equal( SyncState.Conflict, states[ "Light.Outline" ] );
		Assert.Equal( 87, states.Values.Count( s => s == SyncState.InSync ) );
	}

	[Fact]
	public void Link_IgnoresCase_AndKeepsPenpotsSpelling()
	{
		// the design file names the Navy blue tokens "Navyblue.*"; the code's palette is NavyBlue
		PenpotFile file = FakeFile( spell: n => n.Replace( "NavyBlue.", "Navyblue." ) );
		SyncManifest manifest = new();
		ColorSync.Link( manifest, file );
		ColorLink navy = manifest.Colors.Single( c => c.Palette == "NavyBlue" && c.Role == "Primary" );
		Assert.Equal( "Navyblue.Primary", navy.Token );
		Assert.NotNull( navy.TokenId );
		Assert.All( ColorSync.Compare( manifest, file ), r => Assert.Equal( SyncState.InSync, r.State ) );
		// linking again finds the same links instead of adding new ones
		Assert.Equal( 0, ColorSync.Link( manifest, file ) );
	}

	[Fact]
	public void Compare_ReportsATokenDeletedInPenpot()
	{
		SyncManifest manifest = LinkedManifest( FakeFile() );
		ColorReport report = ColorSync.Compare( manifest, FakeFile( missing: [ "NavyBlue.Tertiary" ] ) ).Single( r => r.Link.Token == "NavyBlue.Tertiary" );
		Assert.Equal( SyncState.MissingInPenpot, report.State );
	}

	[Fact]
	public void Manifest_RoundTrips_InStableOrder()
	{
		string path = Path.Combine( Path.GetTempPath(), $"penpot-sync-{Guid.NewGuid():N}.json" );
		try
		{
			SyncManifest manifest = LinkedManifest( FakeFile() );
			manifest.Colors.Reverse();
			manifest.Save( path );
			SyncManifest read = SyncManifest.Load( path );
			Assert.Equal( FileId, read.Penpot.FileId );
			Assert.Equal( manifest.Colors.Select( c => (c.Token, c.TokenId, c.Synced) ), read.Colors.Select( c => (c.Token, c.TokenId, c.Synced) ) );
			Assert.Equal( "Dark", read.Colors[ 0 ].Palette );
			Assert.Equal( File.ReadAllText( path ), SaveText( read, path ) );
		}
		finally
		{
			File.Delete( path );
		}

		static string SaveText( SyncManifest m, string p ) { m.Save( p ); return File.ReadAllText( p ); }
	}
}
