using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using HackPDM.Core.Theming;

namespace WyrmpdmSync.Sync;

/// <summary>
/// The style properties the sync understands, and how each reads and writes on both sides. Values are compared in a
/// shared form: brushes as a Material role ("SurfaceContainerLow"), thicknesses as "left,top,right,bottom", corner
/// radii and sizes as plain numbers.
/// </summary>
internal static partial class StyleValues
{
	public const string Background = nameof( Background ), BorderBrush = nameof( BorderBrush ), Padding = nameof( Padding ),
		CornerRadius = nameof( CornerRadius ), Width = nameof( Width ), Height = nameof( Height ),
		RowHeight = nameof( RowHeight ), FontSize = nameof( FontSize );

	/// <summary>Every property synced between a Penpot component and a XAML style.</summary>
	public static readonly IReadOnlyList<string> All = [ Background, BorderBrush, Padding, CornerRadius, Width, Height, RowHeight, FontSize ];

	/// <summary>
	/// Properties `push` can't write: resizing a shape through Penpot's API means recomputing its geometry, so sizes are
	/// changed in Penpot and pulled into code.
	/// </summary>
	/// <remarks>
	/// RowHeight (a table style's row height ↔ a row component's height) and FontSize (↔ the size of its first text)
	/// are pull-only for the same reason, and because a text's size lives in each run of its content.
	/// </remarks>
	public static readonly IReadOnlySet<string> PullOnly = new HashSet<string> { Width, Height, RowHeight, FontSize };

	/// <summary>Brush resources that stand for a role without being named after it.</summary>
	private static readonly Dictionary<string, string> BrushAliases = new( StringComparer.Ordinal )
	{
		[ "HackPanelStrokeBrush" ] = MaterialRoles.OutlineVariant,
	};

	// ---- code (XAML setter text) ----

	/// <summary>A XAML setter value in the shared form; null when it isn't one the sync understands.</summary>
	public static string? FromXaml( string property, string? text )
	{
		if( text is null )
			return null;
		return property switch
		{
			Background or BorderBrush => BrushRole( text ),
			Padding => Thickness( text ),
			CornerRadius => Uniform( text ),
			Width or Height or RowHeight or FontSize => Number.Normalize( text ),
			_ => null,
		};
	}

	/// <summary>The XAML text for a value, keeping <paramref name="current"/> when it already means the same.</summary>
	public static string ToXaml( string property, string value, string current )
	{
		if( FromXaml( property, current ) == value )
			return current;
		return property switch
		{
			Background or BorderBrush => MaterialRoles.IsRole( value ) ? $"{{ThemeResource {MaterialRoles.BrushKey( value )}}}" : value,
			Padding => CompactThickness( value ),
			_ => value,
		};
	}

	private static string? BrushRole( string text )
	{
		Match m = ResourceRegex().Match( text );
		if( !m.Success )
			return text.Trim();
		string key = m.Groups[ "key" ].Value;
		if( BrushAliases.TryGetValue( key, out string? alias ) )
			return alias;
		string role = key.EndsWith( "Brush", StringComparison.Ordinal ) ? key[ ..^5 ] : key;
		if( role.StartsWith( "Hack", StringComparison.Ordinal ) && MaterialRoles.IsRole( role[ 4.. ] ) )
			role = role[ 4.. ];
		return MaterialRoles.IsRole( role ) ? role : key;
	}

	/// <summary>"16,8" → "16,8,16,8"; "8" → "8,8,8,8"; "8,4,12,4" stays.</summary>
	private static string? Thickness( string text )
	{
		string?[] parts = [ .. text.Split( ',' ).Select( p => Number.Normalize( p ) ) ];
		if( parts.Any( p => p is null ) )
			return null;
		return parts.Length switch
		{
			1 => $"{parts[ 0 ]},{parts[ 0 ]},{parts[ 0 ]},{parts[ 0 ]}",
			2 => $"{parts[ 0 ]},{parts[ 1 ]},{parts[ 0 ]},{parts[ 1 ]}",
			4 => string.Join( ",", parts ),
			_ => null,
		};
	}

	/// <summary>The shortest XAML form of "left,top,right,bottom".</summary>
	private static string CompactThickness( string ltrb )
	{
		string[] p = ltrb.Split( ',' );
		if( p.All( x => x == p[ 0 ] ) )
			return p[ 0 ];
		if( p[ 0 ] == p[ 2 ] && p[ 1 ] == p[ 3 ] )
			return $"{p[ 0 ]},{p[ 1 ]}";
		return ltrb;
	}

	/// <summary>A corner radius with one value for all corners; null when the corners differ.</summary>
	private static string? Uniform( string text )
	{
		string?[] parts = [ .. text.Split( ',' ).Select( p => Number.Normalize( p ) ) ];
		return parts.Length > 0 && parts.All( p => p is not null && p == parts[ 0 ] ) ? parts[ 0 ] : null;
	}

	// ---- Penpot (a component's main shape) ----

	/// <summary>A shape's value in the shared form, or null when the shape doesn't have one.</summary>
	public static string? FromShape( string property, JsonObject shape, string theme, JsonObject? objects = null )
	{
		switch( property )
		{
			case RowHeight:
				return Number.Normalize( shape[ "height" ]?.ToString() );
			case FontSize:
				// the component's main text: its largest (a select's value, not its small label)
				IEnumerable<JsonObject> texts = shape[ "type" ]?.ToString() == "text" ? [ shape ]
					: objects is null ? [] : Penpot.ShapeTree.Descendants( objects, shape ).Where( s => s[ "type" ]?.ToString() == "text" );
				double? largest = texts.Select( t => double.TryParse( t[ "content" ]?[ "children" ]?[ 0 ]?[ "children" ]?[ 0 ]?[ "children" ]?[ 0 ]?[ "font-size" ]?.ToString(),
						System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double size ) ? size : (double?)null )
					.Max();
				return largest is { } l ? Number.Normalize( l.ToString( System.Globalization.CultureInfo.InvariantCulture ) ) : null;
			case Background:
				return Paint( shape, "fill", "fills", "fill-color", theme );
			case BorderBrush:
				return Paint( shape, "stroke-color", "strokes", "stroke-color", theme );
			case Padding:
				if( shape[ "layout-padding" ] is not JsonObject pad )
					return null;
				// Penpot: p1 top, p2 right, p3 bottom, p4 left
				return string.Join( ",", new[] { "p4", "p1", "p2", "p3" }.Select( k => Number.Normalize( pad[ k ]?.ToString() ) ?? "0" ) );
			case CornerRadius:
				string?[] r = [ .. new[] { "r1", "r2", "r3", "r4" }.Select( k => shape[ k ] is null ? null : Number.Normalize( shape[ k ]!.ToString() ) ) ];
				string? first = r.FirstOrDefault( v => v is not null );
				return first is not null && r.All( v => v is null || v == first ) ? first : null;
			case Width or Height:
				return Number.Normalize( shape[ property.ToLowerInvariant() ]?.ToString() );
			default:
				return null;
		}
	}

	/// <summary>The role of the token applied to a paint, else its plain color, else "Transparent" when there is none.</summary>
	private static string? Paint( JsonObject shape, string tokenAttr, string listKey, string colorKey, string theme )
	{
		if( ( shape[ "applied-tokens" ] ?? shape[ "appliedTokens" ] )?[ tokenAttr ]?.ToString() is { } token )
		{
			string prefix = theme + ".";
			return token.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) ? token[ prefix.Length.. ] : token;
		}
		if( shape[ listKey ] is JsonArray list && list.Count > 0 )
			return Hex.Normalize( list[ 0 ]?[ colorKey ]?.ToString() );
		return "Transparent";
	}

	[GeneratedRegex( @"^\s*\{\s*(?:ThemeResource|StaticResource)\s+(?<key>\w+)\s*\}\s*$" )]
	private static partial Regex ResourceRegex();

	/// <summary>A number as JSON for Penpot.</summary>
	public static JsonValue Json( string number ) => JsonValue.Create( double.Parse( number, CultureInfo.InvariantCulture ) );
}
