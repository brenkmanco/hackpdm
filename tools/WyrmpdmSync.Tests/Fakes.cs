using System.Text.Json.Nodes;

using WyrmpdmSync.Penpot;
using WyrmpdmSync.Sync;

namespace WyrmpdmSync.Tests;

/// <summary>Test data: the real ThemePresets.cs, and Penpot files built from it the way the design file is.</summary>
internal static class Fakes
{
	public static readonly Guid FileId = Guid.Parse( "3db45ae8-1dfd-81ce-8009-0028caf8f15e" );
	public static readonly Guid PageId = Guid.Parse( "11111111-1111-1111-1111-111111111111" );

	/// <summary>The repository's ThemePresets.cs, read into memory (tests never write it).</summary>
	public static ThemePresetsSource Code() => ThemePresetsSource.FromText( System.IO.File.ReadAllText(
		Path.Combine( RepoPaths.Find( AppContext.BaseDirectory ).Root, ThemePresetsSource.RelativePath ) ) );

	/// <summary>
	/// A Penpot file holding a color token for every preset color in <paramref name="code"/>, as the design file
	/// does (no shape-value tokens yet), with optional overrides, renames, missing tokens and extra shapes.
	/// </summary>
	public static PenpotFile File( ThemePresetsSource code, Dictionary<string, string>? overrides = null,
		Func<string, string>? spell = null, string[]? missing = null, JsonObject? objects = null, JsonObject? libraryColors = null )
	{
		JsonObject sets = [];
		foreach( IGrouping<HackPDM.Core.Theming.PaletteName, KeyValuePair<(HackPDM.Core.Theming.PaletteName Palette, string Role), SourceLiteral>> group
		         in code.Colors.GroupBy( kv => kv.Key.Palette ) )
		{
			JsonObject tokens = [];
			foreach( ((HackPDM.Core.Theming.PaletteName palette, string role), SourceLiteral _) in group )
			{
				string name = $"{palette}.{role}";
				if( missing?.Contains( name ) == true )
					continue;
				string penpotName = spell?.Invoke( name ) ?? name;
				tokens[ penpotName ] = new JsonObject
				{
					[ "id" ] = Id( penpotName ).ToString(), [ "name" ] = penpotName, [ "type" ] = "color",
					[ "value" ] = overrides?.GetValueOrDefault( name ) ?? code.Color( palette, role ),
				};
			}
			string title = HackPDM.Core.Theming.ThemePresets.Palette( group.Key ).Title;
			sets[ $"S-{title}" ] = new JsonObject
			{
				[ "id" ] = Id( $"set {title}" ).ToString(), [ "name" ] = $"HackPDM colors/{title}", [ "tokens" ] = tokens,
			};
		}
		return PenpotFile.Parse( new JsonObject
		{
			[ "id" ] = FileId.ToString(), [ "name" ] = "HackPDM -- Uno Materials Design", [ "revn" ] = 42, [ "vern" ] = 0,
			[ "features" ] = new JsonArray( "components/v2", "design-tokens/v1" ),
			[ "data" ] = new JsonObject
			{
				[ "pages" ] = new JsonArray( PageId.ToString() ),
				[ "pages-index" ] = new JsonObject
				{
					[ PageId.ToString() ] = new JsonObject { [ "name" ] = "Colors", [ "objects" ] = objects ?? [] },
				},
				[ "colors" ] = libraryColors ?? [],
				[ "tokens-status" ] = new JsonObject { [ "active-set-ids" ] = new JsonArray( Id( "set Light" ).ToString() ) },
				// nested a level deep on purpose: the reader finds sets by their shape, not by a fixed path
				[ "tokens-lib" ] = new JsonObject { [ "sets" ] = new JsonObject { [ "G-HackPDM colors" ] = sets } },
			},
		} );
	}

	/// <summary>A stable id for a name, so tests can refer to the same token across files.</summary>
	public static Guid Id( string name )
	{
		byte[] hash = System.Security.Cryptography.MD5.HashData( System.Text.Encoding.UTF8.GetBytes( name ) );
		return new Guid( hash );
	}

	public static SyncManifest Linked( PenpotFile file, ThemePresetsSource code )
	{
		SyncManifest manifest = new() { Penpot = { FileId = FileId } };
		TokenSync.Link( manifest, file, code );
		return manifest;
	}
}
