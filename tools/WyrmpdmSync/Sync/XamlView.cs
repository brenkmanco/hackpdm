using System.Text;
using System.Text.RegularExpressions;

namespace WyrmpdmSync.Sync;

/// <summary>An attribute of a XAML element: its value and where the value sits in the file.</summary>
internal sealed record XamlAttribute( string Name, SourceLiteral Value );

/// <summary>An element of a XAML view, as found in the text.</summary>
internal sealed class XamlElement
{
	public required string Tag { get; init; }
	/// <summary>The element's <c>pp:Id</c> marker, if it has one.</summary>
	public string? Anchor { get; init; }
	public required Dictionary<string, XamlAttribute> Attributes { get; init; }
	/// <summary>Where the start tag begins.</summary>
	public int Start { get; init; }
	/// <summary>Where the start tag ends (just after its '>'), or -1 for a self-closing element.</summary>
	public int ContentStart { get; init; }
	/// <summary>Where the end tag begins (where new children go), or -1 for a self-closing element.</summary>
	public int ContentEnd { get; set; } = -1;
	/// <summary>The whitespace before the start tag on its line.</summary>
	public required string Indent { get; init; }
	public XamlElement? Parent { get; init; }
	public List<XamlElement> Children { get; } = [];

	public string? Attr( string name ) => Attributes.TryGetValue( name, out XamlAttribute? a ) ? a.Value.Text : null;
	public bool SelfClosing => ContentStart < 0;

	/// <summary>The nearest ancestor with an anchor.</summary>
	public XamlElement? AnchoredParent
	{
		get
		{
			for( XamlElement? p = Parent; p is not null; p = p.Parent )
				if( p.Anchor is not null )
					return p;
			return null;
		}
	}

	/// <summary>The element's own children (not property elements such as &lt;Grid.RowDefinitions&gt;).</summary>
	public IEnumerable<XamlElement> ContentChildren => Children.Where( c => !c.Tag.Contains( '.' ) );
}

/// <summary>
/// A XAML view file read as text. Elements marked <c>pp:Id="…"</c> are anchors the layout sync links to Penpot
/// shapes. Edits replace attribute values, add attributes, or insert elements, leaving everything else as written.
/// </summary>
internal sealed partial class XamlView
{
	public const string AnchorAttribute = "pp:Id";
	public const string Namespace = "urn:wyrmpdm:penpot-sync";

	private readonly string _path;
	private string _text;

	public List<XamlElement> Elements { get; } = [];
	public Dictionary<string, XamlElement> Anchors { get; } = new( StringComparer.Ordinal );

	private XamlView( string path, string text )
	{
		_path = path;
		_text = text;
		Parse();
	}

	public static XamlView Load( string path )
		=> File.Exists( path ) ? new XamlView( path, File.ReadAllText( path ) ) : throw new SyncException( $"{path} not found." );

	public static XamlView FromText( string text ) => new( "", text );

	public string Text => _text;
	public string Path => _path;

	/// <summary>Whether the file declares the pp namespace as ignorable (needed before adding markers).</summary>
	public bool DeclaresNamespace => _text.Contains( Namespace, StringComparison.Ordinal );

	/// <summary>A pending text edit: replace <c>Length</c> characters at <c>Start</c> with <c>Text</c>.</summary>
	public sealed record Edit( int Start, int Length, string Text, string? Expected = null );

	/// <summary>Applies edits (positions refer to the current text) and re-reads the file.</summary>
	public void Apply( IEnumerable<Edit> edits )
	{
		StringBuilder sb = new( _text );
		foreach( Edit e in edits.OrderByDescending( e => e.Start ).ThenByDescending( e => e.Length ) )
		{
			if( e.Expected is not null && _text.Substring( e.Start, e.Length ) != e.Expected )
				throw new SyncException( $"{System.IO.Path.GetFileName( _path )} changed while syncing; run the command again." );
			sb.Remove( e.Start, e.Length ).Insert( e.Start, e.Text );
		}
		_text = sb.ToString();
		Parse();
	}

	public void Save()
	{
		if( _path.Length == 0 )
			throw new InvalidOperationException( "This view was not loaded from disk." );
		File.WriteAllText( _path, _text );
	}

	/// <summary>The edit that sets an attribute: replaces its value, or adds it after the tag name.</summary>
	public Edit SetAttribute( XamlElement element, string name, string value )
	{
		if( element.Attributes.TryGetValue( name, out XamlAttribute? existing ) )
			return new Edit( existing.Value.Start, existing.Value.Length, Escape( value ), existing.Value.Text );
		int at = element.Start + 1 + element.Tag.Length;
		return new Edit( at, 0, $" {name}=\"{Escape( value )}\"" );
	}

	/// <summary>The edit that declares the pp namespace on the root element and marks it ignorable.</summary>
	public IEnumerable<Edit> DeclareNamespace()
	{
		if( DeclaresNamespace || Elements.Count == 0 )
			yield break;
		XamlElement root = Elements[ 0 ];
		int at = root.Start + 1 + root.Tag.Length;
		yield return new Edit( at, 0, $"\n\txmlns:pp=\"{Namespace}\"" );
		if( root.Attributes.TryGetValue( "mc:Ignorable", out XamlAttribute? ignorable ) )
			yield return new Edit( ignorable.Value.Start, ignorable.Value.Length, ignorable.Value.Text + " pp", ignorable.Value.Text );
		else
			yield return new Edit( at, 0, "\n\txmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"\n\tmc:Ignorable=\"pp\"" );
	}

	public static string Escape( string value ) => value.Replace( "&", "&amp;" ).Replace( "\"", "&quot;" ).Replace( "<", "&lt;" );

	public static string Unescape( string value )
		=> value.Replace( "&quot;", "\"" ).Replace( "&lt;", "<" ).Replace( "&gt;", ">" ).Replace( "&apos;", "'" ).Replace( "&amp;", "&" );

	private void Parse()
	{
		Elements.Clear();
		Anchors.Clear();
		Stack<XamlElement> open = new();
		foreach( Match m in TagRegex().Matches( _text ) )
		{
			if( m.Value.StartsWith( "<!--", StringComparison.Ordinal ) || m.Value.StartsWith( "<?", StringComparison.Ordinal ) )
				continue;
			if( m.Groups[ "close" ].Success )
			{
				if( open.Count > 0 )
					open.Pop().ContentEnd = m.Index;
				continue;
			}
			string tag = m.Groups[ "tag" ].Value;
			Group attrs = m.Groups[ "attrs" ];
			Dictionary<string, XamlAttribute> attributes = new( StringComparer.Ordinal );
			foreach( Match a in AttrRegex().Matches( attrs.Value ) )
			{
				Group v = a.Groups[ "v" ];
				attributes[ a.Groups[ "name" ].Value ] = new XamlAttribute( a.Groups[ "name" ].Value,
					new SourceLiteral( attrs.Index + v.Index, v.Length, v.Value ) );
			}
			bool selfClosing = m.Groups[ "self" ].Success;
			int lineStart = _text.LastIndexOf( '\n', Math.Max( 0, m.Index - 1 ) ) + 1;
			string before = _text[ lineStart..m.Index ];
			XamlElement element = new()
			{
				Tag = tag,
				Anchor = attributes.TryGetValue( AnchorAttribute, out XamlAttribute? anchor ) ? anchor.Value.Text : null,
				Attributes = attributes,
				Start = m.Index,
				ContentStart = selfClosing ? -1 : m.Index + m.Length,
				Indent = before.Trim().Length == 0 ? before : "",
				Parent = open.Count > 0 ? open.Peek() : null,
			};
			element.Parent?.Children.Add( element );
			Elements.Add( element );
			if( element.Anchor is not null && !Anchors.TryAdd( element.Anchor, element ) )
				throw new SyncException( $"{System.IO.Path.GetFileName( _path )}: pp:Id \"{element.Anchor}\" is used twice." );
			if( !selfClosing )
				open.Push( element );
		}
	}

	[GeneratedRegex( @"<!--.*?-->|<\?.*?\?>|<(?<close>/)[\w:.]+\s*>|<(?<tag>[\w:.]+)(?<attrs>(?:\s+[\w:.]+\s*=\s*""[^""]*"")*)\s*(?<self>/)?>", RegexOptions.Singleline )]
	private static partial Regex TagRegex();

	[GeneratedRegex( @"(?<name>[\w:.]+)\s*=\s*""(?<v>[^""]*)""" )]
	private static partial Regex AttrRegex();
}
