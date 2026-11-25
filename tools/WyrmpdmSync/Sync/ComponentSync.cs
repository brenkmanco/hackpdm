using System.Text.Json.Nodes;

using WyrmpdmSync.Penpot;

namespace WyrmpdmSync.Sync;

/// <summary>One synced setter of a component link.</summary>
internal sealed record ComponentReport( ComponentLink Link, string Property, string? Penpot, string? Code, SyncState State );

/// <summary>Links Penpot components to XAML styles, compares their setters, and works out pulls and pushes.</summary>
internal static class ComponentSync
{
	/// <summary>The theme variant compared with the code; a push writes every theme.</summary>
	public const string SourceTheme = "Light";

	/// <summary>
	/// Adds the default component ↔ style pairs that are missing, and for each synced link the setters its style
	/// has that the sync understands. A setter whose two sides already match gets that value as its baseline.
	/// Returns how many links were added.
	/// </summary>
	public static int Link( SyncManifest manifest, PenpotFile file, Func<string, XamlStyles> xaml )
	{
		int added = 0;
		foreach( ComponentLink seed in ComponentMap.Defaults )
		{
			if( manifest.Components.Any( c => c.Style == seed.Style ) )
				continue;
			manifest.Components.Add( seed );
			added++;
		}
		foreach( ComponentLink link in manifest.Components.Where( c => !c.IsReference ) )
		{
			XamlStyles styles = xaml( link.File! );
			if( !styles.Styles.TryGetValue( link.Style, out Dictionary<string, SourceLiteral>? setters ) )
				continue;
			JsonObject? shape = MainShape( file, link, SourceTheme );
			foreach( string property in StyleValues.All.Where( setters.ContainsKey ) )
			{
				if( link.Properties.ContainsKey( property ) )
					continue;
				string? code = StyleValues.FromXaml( property, setters[ property ].Text );
				string? penpot = shape is null ? null : StyleValues.FromShape( property, shape, SourceTheme );
				link.Properties[ property ] = code is not null && code == penpot ? code : null;
			}
		}
		return added;
	}

	public static List<ComponentReport> Compare( SyncManifest manifest, PenpotFile file, Func<string, XamlStyles> xaml )
	{
		List<ComponentReport> reports = [];
		foreach( ComponentLink link in manifest.Components.Where( c => !c.IsReference ) )
		{
			XamlStyles styles = xaml( link.File! );
			JsonObject? shape = MainShape( file, link, SourceTheme );
			foreach( (string property, string? synced) in link.Properties )
			{
				string? code = StyleValues.FromXaml( property, styles.Value( link.Style, property ) );
				string? penpot = shape is null ? null : StyleValues.FromShape( property, shape, SourceTheme );
				reports.Add( new ComponentReport( link, property, penpot, code, ThreeWay.Compare( penpot, code, synced ) ) );
			}
		}
		return reports;
	}

	public static List<ComponentReport> ToPull( IEnumerable<ComponentReport> reports, Prefer prefer )
		=> [ .. reports.Where( r => r.Penpot is not null && ( r.State == SyncState.ChangedInPenpot
		                             || ( prefer == Prefer.Penpot && r.State is SyncState.Conflict or SyncState.Unlinked ) ) ) ];

	public static List<ComponentReport> ToPush( IEnumerable<ComponentReport> reports, Prefer prefer )
		=> [ .. reports.Where( r => r.Code is not null && !StyleValues.PullOnly.Contains( r.Property )
		                             && ( r.State == SyncState.ChangedInCode
		                                  || ( prefer == Prefer.Code && r.State is SyncState.Conflict or SyncState.Unlinked ) ) ) ];

	/// <summary>Writes pulled values into the XAML styles (in memory; the caller saves).</summary>
	public static void ApplyToCode( IReadOnlyList<ComponentReport> pulled, Func<string, XamlStyles> xaml )
	{
		foreach( IGrouping<string, ComponentReport> byFile in pulled.GroupBy( r => r.Link.File! ) )
		{
			XamlStyles styles = xaml( byFile.Key );
			List<(SourceLiteral, string)> edits = [];
			foreach( ComponentReport r in byFile )
			{
				SourceLiteral literal = styles.Styles[ r.Link.Style ][ r.Property ];
				edits.Add( (literal, StyleValues.ToXaml( r.Property, r.Penpot!, literal.Text )) );
			}
			styles.Apply( edits );
		}
	}

	/// <summary>The main shape of the link's component in <paramref name="theme"/>.</summary>
	public static JsonObject? MainShape( PenpotFile file, ComponentLink link, string theme )
		=> Component( file, link, theme ) is { } c ? file.MainShape( c ) : null;

	public static PenpotComponent? Component( PenpotFile file, ComponentLink link, string theme )
		=> file.Components.FirstOrDefault( c => c.Name == link.Component
			&& link.Variant.All( kv => c.Variant.GetValueOrDefault( kv.Key ) == kv.Value )
			&& ( !c.Variant.ContainsKey( "Theme" ) || c.Variant[ "Theme" ] == theme ) );

	/// <summary>The themes the link's component has (Light, Dark…).</summary>
	public static IEnumerable<string> Themes( PenpotFile file, ComponentLink link )
		=> file.Components.Where( c => c.Name == link.Component && link.Variant.All( kv => c.Variant.GetValueOrDefault( kv.Key ) == kv.Value ) )
			.Select( c => c.Variant.GetValueOrDefault( "Theme" ) ?? SourceTheme ).Distinct();
}
