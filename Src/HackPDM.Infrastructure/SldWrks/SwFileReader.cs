using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using HackPDM.Infrastructure.SldWrks.Format;

namespace HackPDM.Infrastructure.SldWrks;

/// <summary>
/// Reads SolidWorks references and custom properties straight from the file, on any OS, without
/// SolidWorks or its Document Manager. Works on 2014-and-earlier (OLE) and 2015+ (chunked) files.
/// </summary>
public sealed partial class SwFileReader : ISwDocumentReader
{
	private static SwStreamStore? OpenStore( string fileName, bool noInterrupt )
	{
		if( !SwDocumentTypes.IsSolidWorks( fileName ) )
			return null;
		try
		{
			var store = SwStreamStore.Open( fileName );
			if( store.Format == SwStreamStore.ContainerFormat.Unknown )
				throw new InvalidDataException( $"not a recognised SolidWorks file format: {fileName}" );
			return store;
		}
		catch( Exception e ) when( noInterrupt && e is IOException or UnauthorizedAccessException or InvalidDataException )
		{
			return null;
		}
	}

	#region References

	/// <summary>Lists the files a SolidWorks document references, read straight from the file (see <see cref="GetReferences"/>).</summary>
	/// <param name="fileName">Path of the SolidWorks file; read fully into memory.</param>
	/// <param name="deep">Ignored; only direct references are returned.</param>
	/// <param name="noInterrupt">When true, an unreadable or unrecognised file yields null instead of an exception.</param>
	/// <returns>
	/// <c>[file name, stored path]</c> pairs without duplicates, virtual components or the document itself; null when the
	/// file isn't a SolidWorks document by extension or, with <paramref name="noInterrupt"/>, can't be read.
	/// </returns>
	/// <exception cref="IOException">The file can't be read (including <see cref="FileNotFoundException"/>) and <paramref name="noInterrupt"/> is false.</exception>
	/// <exception cref="UnauthorizedAccessException">Access to the file is denied and <paramref name="noInterrupt"/> is false.</exception>
	/// <exception cref="InvalidDataException">The file is not a recognised SolidWorks container or its OLE structure is corrupt, and <paramref name="noInterrupt"/> is false.</exception>
	public List<string[]>? GetDependencies( string fileName, bool deep = false, bool noInterrupt = false )
	{
		if( OpenStore( fileName, noInterrupt ) is not { } store )
			return null;
		return [ .. GetReferences( store, Path.GetFileName( fileName ) ).Select( path => new[] { FileNameOf( path ), path } ) ];
	}

	/// <summary>
	/// Extracts the paths of the files a document references, in stored order, without duplicates, virtual
	/// components (names containing <c>^</c>) or the document itself.
	/// </summary>
	/// <param name="store">The document's streams.</param>
	/// <param name="ownFileName">The document's current file name, so references to itself are dropped; null to keep them.</param>
	/// <returns>The stored (Windows) paths; empty when no reference stream is found.</returns>
	/// <remarks>
	/// Sources, first match wins:
	/// <list type="number">
	/// <item>"SwDocContentMgr/SwDocContentMgrInfo" (2015+): the direct references; the first path in
	/// it is the document itself as it was saved (possibly under an older name) and is skipped.</item>
	/// <item>Otherwise "Header2", which can also hold stale references (files no longer used, not
	/// in the component tree), so when "swXmlContents/COMPINSTANCETREE" exists only paths also
	/// in that tree are kept.</item>
	/// <item>Otherwise the component tree itself.</item>
	/// </list>
	/// </remarks>
	public static List<string> GetReferences( SwStreamStore store, string? ownFileName = null )
	{
		var paths = new List<string>();
		var seen = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		bool IsSelf( string path ) => ownFileName is not null && FileNameOf( path ).Equals( ownFileName, StringComparison.OrdinalIgnoreCase );
		void AddAll( IEnumerable<string> found )
		{
			foreach( string path in found )
				if( !path.Contains( '^' ) && !IsSelf( path ) && seen.Add( path ) ) // "Part1^Assem1.sldprt" is a virtual component
					paths.Add( path );
		}

		var tree = store.GetText( "swXmlContents/COMPINSTANCETREE" ) is { } xml ? TreePaths( xml ).ToList() : null;
		if( store.Get( "SwDocContentMgr/SwDocContentMgrInfo" ) is { } contentMgr )
		{
			var found = Utf16Paths( contentMgr ).ToList();
			if( found.Count > 0 )
				seen.Add( found[ 0 ] );
			AddAll( found );
		}
		else if( store.Get( "Header2" ) is { } header )
		{
			var inTree = tree is null ? null : new HashSet<string>( tree, StringComparer.OrdinalIgnoreCase );
			AddAll( Utf16Paths( header ).Where( p => inTree?.Contains( p ) ?? true ) );
		}
		else if( tree is not null )
		{
			AddAll( tree );
		}
		return paths;
	}

	[GeneratedRegex( @"(?:[\x20-\x7E]\x00)+?\.\x00[sS]\x00[lL]\x00[dD]\x00(?:[pP]\x00[rR]\x00[tT]|[aA]\x00[sS]\x00[mM]|[dD]\x00[rR]\x00[wW])\x00" )]
	private static partial Regex Utf16PathPattern();

	/// <summary>UTF-16 strings ending in a SolidWorks extension, in order.</summary>
	private static IEnumerable<string> Utf16Paths( byte[] stream )
	{
		// match on the bytes as Latin-1 so each byte is one char
		string bytes = Encoding.Latin1.GetString( stream );
		foreach( Match m in Utf16PathPattern().Matches( bytes ) )
			yield return Encoding.Unicode.GetString( Encoding.Latin1.GetBytes( m.Value ) );
	}

	private static IEnumerable<string> TreePaths( string xml )
	{
		XDocument doc;
		try { doc = XDocument.Parse( xml ); }
		catch( System.Xml.XmlException ) { yield break; }
		foreach( var file in doc.Descendants().Where( e => e.Name.LocalName == "swFile" ) )
			if( file.Attribute( "swPath" )?.Value is { Length: > 0 } path )
				yield return path;
	}

	private static string FileNameOf( string storedPath )
		=> storedPath[ ( storedPath.LastIndexOfAny( [ '\\', '/' ] ) + 1 ).. ];

	#endregion

	#region Preview

	private static ReadOnlySpan<byte> PngSignature => [ 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A ];

	/// <summary>Reads the preview image saved in a SolidWorks file.</summary>
	/// <param name="fileName">Path of the SolidWorks file.</param>
	/// <returns>The PNG bytes, or null when there is none, the file isn't a SolidWorks file, or it can't be read.</returns>
	public byte[]? GetPreviewPng( string fileName )
		=> OpenStore( fileName, noInterrupt: true ) is { } store ? GetPreviewPng( store ) : null;

	/// <summary>Gets the "PreviewPNG" stream (stored by SolidWorks 2009 and later) when it really is a PNG.</summary>
	/// <param name="store">The document's streams.</param>
	/// <returns>The PNG bytes, or null when the stream is missing or doesn't start with the PNG signature.</returns>
	public static byte[]? GetPreviewPng( SwStreamStore store )
		=> store.Get( "PreviewPNG" ) is { } png && png.AsSpan().StartsWith( PngSignature ) ? png : null;

	#endregion
	#region Properties

	/// <summary>Reads a SolidWorks file's custom properties straight from the file (see <see cref="ReadProperties"/>).</summary>
	/// <param name="fileName">Path of the SolidWorks file; read fully into memory.</param>
	/// <returns>(configuration, name, type, value) tuples as described on <see cref="ISwDocumentReader.GetProperties"/>; null when the file isn't a SolidWorks document by extension.</returns>
	/// <exception cref="IOException">The file can't be read.</exception>
	/// <exception cref="UnauthorizedAccessException">Access to the file is denied.</exception>
	/// <exception cref="InvalidDataException">The file is not a recognised SolidWorks container or its OLE structure is corrupt.</exception>
	public List<Tuple<string, string, string, object>>? GetProperties( string fileName )
	{
		if( OpenStore( fileName, noInterrupt: false ) is not { } store )
			return null;
		var result = new List<Tuple<string, string, string, object>>();
		foreach( var p in ReadProperties( store ) )
			result.Add( Tuple.Create( p.Configuration, p.Name, p.Type, p.Value ) );
		return result;
	}

	/// <summary>One custom property read from a SolidWorks file.</summary>
	/// <param name="Configuration">The configuration name, or "" for a file-level property.</param>
	/// <param name="Name">The property name.</param>
	/// <param name="Type">"text", "number", "date", "yesno", or "" when the type isn't recognised.</param>
	/// <param name="Value">The value: string, decimal, DateTime or bool to match <paramref name="Type"/>; the raw text or value otherwise.</param>
	public readonly record struct SwProperty( string Configuration, string Name, string Type, object Value );

	[GeneratedRegex( @"^docProps/Config-(\d+)-Properties\.xml$" )]
	private static partial Regex ConfigXmlStream();

	[GeneratedRegex( @"^Config-(\d+)-Properties$" )]
	private static partial Regex ConfigOleStream();

	/// <summary>Reads file-level custom properties (configuration "") followed by each configuration's, in configuration index order.</summary>
	/// <param name="store">The document's streams.</param>
	/// <returns>The properties; empty when the file has none. Unparseable XML or property-set streams are skipped.</returns>
	/// <remarks>
	/// 2015+ files store properties as XML ("docProps/custom.xml", "docProps/Config-N-Properties.xml"); for those, a
	/// property's evaluated value (for example of "SW-Mass@Part1.SLDPRT") is used when stored. Older files use OLE property
	/// sets ("\u0005DocumentSummaryInformation", "Config-N-Properties"), read with <see cref="OlePropertySet"/>.
	/// Configuration names come from <see cref="ConfigurationNames"/>.
	/// </remarks>
	public static List<SwProperty> ReadProperties( SwStreamStore store )
	{
		var result = new List<SwProperty>();

		// file level: 2015+ XML, else the OLE user-defined property set
		if( store.GetText( "docProps/custom.xml" ) is { } customXml )
			result.AddRange( FromXml( customXml, "" ) );
		else if( store.Get( "\u0005DocumentSummaryInformation" ) is { } summary )
			result.AddRange( FromPropertySet( summary, "" ) );

		// configurations: "docProps/Config-N-Properties.xml" (2015+) or "Config-N-Properties" (OLE)
		var configStreams = new SortedDictionary<int, (string Name, bool Xml)>();
		foreach( string name in store.Names )
		{
			if( ConfigXmlStream().Match( name ) is { Success: true } x )
				configStreams[ int.Parse( x.Groups[ 1 ].Value, CultureInfo.InvariantCulture ) ] = ( name, true );
			else if( ConfigOleStream().Match( name ) is { Success: true } o )
				configStreams.TryAdd( int.Parse( o.Groups[ 1 ].Value, CultureInfo.InvariantCulture ), ( name, false ) );
		}
		if( configStreams.Count == 0 )
			return result;

		var names = ConfigurationNames( store, configStreams.Keys );
		foreach( var (index, (stream, xml)) in configStreams )
		{
			string config = names[ index ];
			if( xml )
			{
				if( store.GetText( stream ) is { } text )
					result.AddRange( FromXml( text, config ) );
			}
			else if( store.Get( stream ) is { } bytes )
			{
				result.AddRange( FromPropertySet( bytes, config ) );
			}
		}
		return result;
	}

	/// <summary>
	/// Maps configuration indexes (the N in "Config-N-...") to names, from (in order): the configuration header stream
	/// (<see cref="ConfigHeaderReader.ReadNames"/>), the configuration's cut-list properties, and, for a single-configuration
	/// file, the active configuration name.
	/// </summary>
	/// <param name="store">The document's streams.</param>
	/// <param name="indexes">The configuration indexes to name; enumerated once.</param>
	/// <returns>A name for every index in <paramref name="indexes"/>: "Config-N" when none of the sources names it.</returns>
	public static Dictionary<int, string> ConfigurationNames( SwStreamStore store, IEnumerable<int> indexes )
	{
		var names = ConfigHeaderReader.ReadNames( store );
		var result = new Dictionary<int, string>();
		var list = indexes.ToList();
		foreach( int index in list )
		{
			if( names.TryGetValue( index, out var name ) || ( name = CutListName( store, index ) ) is not null )
				result[ index ] = name;
		}
		if( list.Count == 1 && !result.ContainsKey( list[ 0 ] ) && ActiveConfiguration( store ) is { } active )
			result[ list[ 0 ] ] = active;
		foreach( int index in list )
			result.TryAdd( index, $"Config-{index}" );
		return result;
	}

	private static string? CutListName( SwStreamStore store, int index )
	{
		if( store.GetText( $"docProps/Config-{index}-Cutlist-Properties.xml" ) is not { } xml )
			return null;
		try
		{
			return XDocument.Parse( xml ).Descendants()
				.FirstOrDefault( e => e.Name.LocalName == "Configuration" )?.Attribute( "Name" )?.Value is { Length: > 0 } n ? n : null;
		}
		catch( System.Xml.XmlException ) { return null; }
	}

	private static string? ActiveConfiguration( SwStreamStore store )
	{
		if( store.GetText( "docProps/ISolidWorksInformation.xml" ) is not { } xml )
			return null;
		try
		{
			return XDocument.Parse( xml ).Descendants()
				.Where( e => e.Name.LocalName == "property" && (string?)e.Attribute( "name" ) == "SW-Configuration Name" )
				.Select( e => e.Elements().FirstOrDefault()?.Value )
				.FirstOrDefault( v => !string.IsNullOrEmpty( v ) );
		}
		catch( System.Xml.XmlException ) { return null; }
	}

	/// <summary>
	/// Named properties of the "UserDefinedProperties" section of a 2015+ property XML. The value is
	/// the evaluated one (&lt;FPVals&gt;) when the file has it, e.g. for "SW-Mass@Part1.SLDPRT".
	/// </summary>
	private static IEnumerable<SwProperty> FromXml( string xml, string configuration )
	{
		XDocument doc;
		try { doc = XDocument.Parse( xml ); }
		catch( System.Xml.XmlException ) { yield break; }

		var section = doc.Descendants().FirstOrDefault( e => e.Name.LocalName == "propertySection" && (string?)e.Attribute( "name" ) == "UserDefinedProperties" );
		if( section is null )
			yield break;
		foreach( var property in section.Elements().Where( e => e.Name.LocalName == "property" ) )
		{
			string name = (string?)property.Attribute( "name" ) ?? "";
			if( name.Length == 0 )
				continue;
			var raw = property.Elements().FirstOrDefault( e => e.Name.LocalName != "FPVals" );
			var evaluated = property.Elements().FirstOrDefault( e => e.Name.LocalName == "FPVals" )?.Elements().FirstOrDefault();
			var value = evaluated ?? raw;
			if( value is null )
				continue;
			var (type, converted) = Convert( value.Name.LocalName, value.Value );
			yield return new( configuration, name, type, converted );
		}
	}

	private static IEnumerable<SwProperty> FromPropertySet( byte[] stream, string configuration )
	{
		foreach( var (name, varType, value) in OlePropertySet.ReadUserDefined( stream ) )
		{
			if( name.Length == 0 || value is null )
				continue;
			var (type, converted) = varType switch
			{
				OlePropertySet.VarType.LpStr or OlePropertySet.VarType.LpWStr => ( "text", value ),
				OlePropertySet.VarType.Bool => ( "yesno", value ),
				OlePropertySet.VarType.Date or OlePropertySet.VarType.FileTime => ( "date", value ),
				OlePropertySet.VarType.I2 or OlePropertySet.VarType.I4 or OlePropertySet.VarType.Int
					or OlePropertySet.VarType.UI4 or OlePropertySet.VarType.UInt or OlePropertySet.VarType.I8
					or OlePropertySet.VarType.R4 or OlePropertySet.VarType.R8
					=> ( "number", (object)System.Convert.ToDecimal( value, CultureInfo.InvariantCulture ) ),
				_ => ( "", value ),
			};
			yield return new( configuration, name, type, converted );
		}
	}

	/// <summary>Maps an XML vt: element ("lpstr", "r8", "bool", "filetime", …) and its text to the Document Manager's types.</summary>
	private static (string Type, object Value) Convert( string vt, string text )
	{
		switch( vt )
		{
			case "lpstr" or "lpwstr" or "bstr":
				return ( "text", text );
			case "bool":
				return ( "yesno", text.Trim().ToLowerInvariant() is "yes" or "true" or "1" or "-1" );
			case "filetime" or "date":
				return DateTime.TryParse( text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date )
					? ( "date", date )
					: ( "", text );
			case "i1" or "i2" or "i4" or "i8" or "ui1" or "ui2" or "ui4" or "ui8" or "int" or "uint" or "r4" or "r8" or "decimal" or "cy":
				return decimal.TryParse( text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number )
					? ( "number", number )
					: ( "", text );
			default:
				return ( "", text );
		}
	}

	#endregion
}
