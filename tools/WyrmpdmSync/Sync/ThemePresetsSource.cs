using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using HackPDM.Core.Theming;

namespace WyrmpdmSync.Sync;

/// <summary>A value written in a source file, and where.</summary>
internal sealed record SourceLiteral( int Start, int Length, string Text );

/// <summary>
/// Src/HackPDM.Core/Theming/ThemePresets.cs read as text: the 30 colors of each palette (the arguments of its
/// <c>MaterialRoles.Scheme( … )</c> call, in <see cref="MaterialRoles.Names"/> order) and each form's corner and
/// border values (its named arguments). Edits replace just those literals, so comments and layout stay as written.
/// </summary>
/// <remarks>
/// Reading the file rather than the compiled assembly means `status` and `pull` see exactly what is on disk,
/// including edits that haven't been built yet.
/// </remarks>
internal sealed partial class ThemePresetsSource
{
	public const string RelativePath = "Src/HackPDM.Core/Theming/ThemePresets.cs";

	/// <summary>The form values synced with Penpot, and the Penpot token type each one becomes.</summary>
	public static readonly IReadOnlyList<(string Property, string TokenType)> FormProperties =
	[
		( nameof( AppForm.ControlCornerRadius ), "border-radius" ),
		( nameof( AppForm.OverlayCornerRadius ), "border-radius" ),
		( nameof( AppForm.PanelCornerRadius ), "border-radius" ),
		( nameof( AppForm.PanelBorderThickness ), "stroke-width" ),
	];

	private readonly string _path;
	private string _text;

	public Dictionary<(PaletteName Palette, string Role), SourceLiteral> Colors { get; } = [];
	public Dictionary<(FormName Form, string Property), SourceLiteral> Forms { get; } = [];

	private ThemePresetsSource( string path, string text )
	{
		_path = path;
		_text = text;
		Parse();
	}

	public static ThemePresetsSource Load( RepoPaths paths ) => Load( Path.Combine( paths.Root, RelativePath ) );

	public static ThemePresetsSource Load( string path )
		=> File.Exists( path ) ? new ThemePresetsSource( path, File.ReadAllText( path ) ) : throw new SyncException( $"{path} not found." );

	public static ThemePresetsSource FromText( string text ) => new( "", text );

	public string Text => _text;

	/// <summary>The color of a palette role as Penpot writes it ("#485E92"), or null when the file doesn't define it.</summary>
	public string? Color( PaletteName palette, string role )
		=> Colors.TryGetValue( (palette, role), out SourceLiteral? lit ) ? Hex.FromArgb( ParseArgb( lit.Text ) ) : null;

	/// <summary>A form value as Penpot writes it ("16", "1.5"), or null when the file doesn't define it.</summary>
	public string? FormValue( FormName form, string property )
		=> Forms.TryGetValue( (form, property), out SourceLiteral? lit ) ? Number.Normalize( lit.Text ) : null;

	/// <summary>Replaces the given values and re-reads the text. Nothing is written until <see cref="Save"/>.</summary>
	public void Apply( IEnumerable<(SourceLiteral Literal, string NewText)> edits )
	{
		StringBuilder sb = new( _text );
		foreach( (SourceLiteral lit, string newText) in edits.OrderByDescending( e => e.Literal.Start ) )
		{
			if( _text.Substring( lit.Start, lit.Length ) != lit.Text )
				throw new SyncException( $"{RelativePath} changed while syncing; run the command again." );
			sb.Remove( lit.Start, lit.Length ).Insert( lit.Start, newText );
		}
		_text = sb.ToString();
		Parse();
	}

	public void Save()
	{
		if( _path.Length == 0 )
			throw new InvalidOperationException( "This source was not loaded from a file." );
		File.WriteAllText( _path, _text );
	}

	/// <summary>A color as the file writes it: 0xAARRGGBB, uppercase.</summary>
	public static string ColorLiteral( string hex )
		=> Hex.ToArgb( hex ) is { } argb ? $"0x{argb:X8}" : throw new SyncException( $"'{hex}' is not a color." );

	private void Parse()
	{
		Colors.Clear();
		Forms.Clear();
		foreach( Match palette in PaletteRegex().Matches( _text ) )
		{
			if( !Enum.TryParse( palette.Groups[ "name" ].Value, out PaletteName name ) )
				continue;
			Group args = palette.Groups[ "args" ];
			MatchCollection literals = HexLiteralRegex().Matches( StripComments( args.Value ) );
			if( literals.Count != MaterialRoles.Names.Count )
				throw new SyncException( $"{RelativePath}: palette {name} lists {literals.Count} colors, expected {MaterialRoles.Names.Count}." );
			for( int i = 0; i < literals.Count; i++ )
				Colors[ (name, MaterialRoles.Names[ i ]) ] = new SourceLiteral( args.Index + literals[ i ].Index, literals[ i ].Length, literals[ i ].Value );
		}
		foreach( Match form in FormRegex().Matches( _text ) )
		{
			if( !Enum.TryParse( form.Groups[ "name" ].Value, out FormName name ) )
				continue;
			Group body = form.Groups[ "body" ];
			foreach( (string property, string _) in FormProperties )
			{
				Match arg = Regex.Match( body.Value, $@"\b{property}\s*:\s*(?<v>-?[0-9]+(?:\.[0-9]+)?)" );
				if( arg.Success )
					Forms[ (name, property) ] = new SourceLiteral( body.Index + arg.Groups[ "v" ].Index, arg.Groups[ "v" ].Length, arg.Groups[ "v" ].Value );
			}
		}
	}

	/// <summary>Blanks out // comments (same length, so positions still line up).</summary>
	private static string StripComments( string s ) => LineCommentRegex().Replace( s, m => new string( ' ', m.Length ) );

	private static uint ParseArgb( string literal ) => uint.Parse( literal.AsSpan( 2 ), NumberStyles.HexNumber, CultureInfo.InvariantCulture );

	// new( PaletteName.Dark, …, MaterialRoles.Scheme( <args> ) )
	[GeneratedRegex( @"new\(\s*PaletteName\.(?<name>\w+)\s*,[^;]*?MaterialRoles\.Scheme\((?<args>[^)]*)\)", RegexOptions.Singleline )]
	private static partial Regex PaletteRegex();

	// new( FormName.Compact, … ),   (the body runs to the closing parenthesis of the record)
	[GeneratedRegex( @"new\(\s*FormName\.(?<name>\w+)\s*,(?<body>[^;]*?AccentPanels\s*:\s*\w+)\s*\)", RegexOptions.Singleline )]
	private static partial Regex FormRegex();

	[GeneratedRegex( @"0x[0-9A-Fa-f]{8}" )]
	private static partial Regex HexLiteralRegex();

	[GeneratedRegex( @"//[^\r\n]*" )]
	private static partial Regex LineCommentRegex();
}

/// <summary>Numbers in Penpot's form: no trailing zeros ("16", "1.5"), invariant culture.</summary>
internal static class Number
{
	public static string? Normalize( string? text )
		=> double.TryParse( text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v )
			? v.ToString( "0.####", CultureInfo.InvariantCulture )
			: null;
}
