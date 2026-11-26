using System.Text;
using System.Text.RegularExpressions;

namespace WyrmpdmSync.Sync;

/// <summary>
/// A XAML resource file read as text: each <c>&lt;Style x:Key="…"&gt;</c> and its <c>&lt;Setter Property="…" Value="…" /&gt;</c>
/// values, with their positions, so a sync can replace one value without reformatting the file.
/// </summary>
internal sealed partial class XamlStyles
{
	public const string HackControlsPath = "Src/HackPDM/Styles/HackControls.xaml";

	private readonly string _path;
	private string _text;

	/// <summary>Style key → property → the Value attribute's text and position.</summary>
	public Dictionary<string, Dictionary<string, SourceLiteral>> Styles { get; } = new( StringComparer.Ordinal );

	private XamlStyles( string path, string text )
	{
		_path = path;
		_text = text;
		Parse();
	}

	public static XamlStyles Load( string path )
		=> File.Exists( path ) ? new XamlStyles( path, File.ReadAllText( path ) ) : throw new SyncException( $"{path} not found." );

	public static XamlStyles FromText( string text ) => new( "", text );

	public string Text => _text;

	/// <summary>A setter's value, or null when the style or setter doesn't exist.</summary>
	public string? Value( string style, string property )
		=> Styles.TryGetValue( style, out Dictionary<string, SourceLiteral>? setters ) && setters.TryGetValue( property, out SourceLiteral? lit ) ? lit.Text : null;

	/// <summary>Replaces setter values (in memory; call <see cref="Save"/> to write) and re-reads the file.</summary>
	public void Apply( IEnumerable<(SourceLiteral Literal, string NewText)> edits )
	{
		StringBuilder sb = new( _text );
		foreach( (SourceLiteral lit, string newText) in edits.OrderByDescending( e => e.Literal.Start ) )
		{
			if( _text.Substring( lit.Start, lit.Length ) != lit.Text )
				throw new SyncException( $"{Path.GetFileName( _path )} changed while syncing; run the command again." );
			sb.Remove( lit.Start, lit.Length ).Insert( lit.Start, newText );
		}
		_text = sb.ToString();
		Parse();
	}

	public void Save()
	{
		if( _path.Length == 0 )
			throw new InvalidOperationException( "This file was not loaded from disk." );
		File.WriteAllText( _path, _text );
	}

	private void Parse()
	{
		Styles.Clear();
		string masked = CommentRegex().Replace( _text, m => new string( ' ', m.Length ) );
		foreach( Match style in StyleRegex().Matches( masked ) )
		{
			string? key = Attr( style.Groups[ "attrs" ].Value, "x:Key" );
			if( key is null )
				continue;
			Dictionary<string, SourceLiteral> setters = new( StringComparer.Ordinal );
			Group body = style.Groups[ "body" ];
			foreach( Match setter in SetterRegex().Matches( body.Value ) )
			{
				string attrs = setter.Groups[ "attrs" ].Value;
				if( Attr( attrs, "Property" ) is not { } property )
					continue;
				Match value = Regex.Match( attrs, @"\bValue\s*=\s*""(?<v>[^""]*)""" );
				if( !value.Success )
					continue;
				int start = body.Index + setter.Groups[ "attrs" ].Index + value.Groups[ "v" ].Index;
				setters[ property ] = new SourceLiteral( start, value.Groups[ "v" ].Length, value.Groups[ "v" ].Value );
			}
			Styles[ key ] = setters;
		}
	}

	private static string? Attr( string attrs, string name )
	{
		Match m = Regex.Match( attrs, $@"(?<![\w:]){Regex.Escape( name )}\s*=\s*""(?<v>[^""]*)""" );
		return m.Success ? m.Groups[ "v" ].Value : null;
	}

	[GeneratedRegex( @"<Style\b(?<attrs>[^>]*)>(?<body>.*?)</Style>", RegexOptions.Singleline )]
	private static partial Regex StyleRegex();

	[GeneratedRegex( @"<Setter\b(?<attrs>[^>]*?)/?>", RegexOptions.Singleline )]
	private static partial Regex SetterRegex();

	[GeneratedRegex( @"<!--.*?-->", RegexOptions.Singleline )]
	private static partial Regex CommentRegex();
}
