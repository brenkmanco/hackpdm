using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Infrastructure.Operations;
using HackPDM.Infrastructure.RPC;
using HackPDM.Infrastructure.SldWrks;

using HackPDM.Shared.GlobalData;

using Newtonsoft.Json;


namespace HackPDM.Infrastructure.Odoo;

public static class ExtensionOdoo
{
	// xmlrpc
	private static readonly Encoding Encoding = new ASCIIEncoding();



	public static ArrayList GetIDs( this IEnumerable<HpBaseModel> models )
		=> models.Select( model => model.id ).ToArrayList();


	// One pooled client for every RPC call: keeps connections alive between calls instead of
	// opening (and leaving in TIME_WAIT) a new TCP connection per request, and accepts
	// compressed responses. Per-call timeouts use a CancellationToken because
	// HttpClient.Timeout cannot change once a shared client has been used.
	//
	// Odoo's built-in server (Werkzeug, no reverse proxy) answers HTTP/1.0 and closes the socket
	// after every response, but SocketsHttpHandler still returns that connection to the pool. When
	// the next call picks it up before the server's FIN arrives, the call fails with
	// "An error occurred while sending the request -> The response ended prematurely". So once a
	// server has answered HTTP/1.0 without keep-alive, its calls go through a client that never
	// pools connections.
	private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds( 100 );
	private static readonly ConcurrentDictionary<(IWebProxy? Proxy, bool Pooled), HttpClient> HttpClients = new();
	private static readonly ConcurrentDictionary<string, bool> ClosesConnections = new( StringComparer.OrdinalIgnoreCase );

	private static HttpClient CreateHttpClient( IWebProxy? proxy, bool pooled )
	{
		var handler = new SocketsHttpHandler
		{
			AutomaticDecompression = DecompressionMethods.All,
			// pooled: recycle connections so DNS / server address changes are picked up;
			// not pooled: zero lifetime closes each connection after its response
			PooledConnectionLifetime = pooled ? TimeSpan.FromMinutes( 5 ) : TimeSpan.Zero,
			ConnectTimeout = TimeSpan.FromSeconds( 15 ),
		};
		if( proxy is not null )
		{
			handler.Proxy = proxy;
			handler.UseProxy = true;
		}
		return new HttpClient( handler ) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
	}

	private static HttpClient HttpClientFor( IWebProxy? proxy, bool pooled )
		=> HttpClients.GetOrAdd( ( proxy, pooled ), key => CreateHttpClient( key.Proxy, key.Pooled ) );

	/// <summary>Whether the server behind <paramref name="url"/> has answered HTTP/1.0 without keep-alive.</summary>
	internal static bool ServerClosesConnections( string url )
		=> ClosesConnections.ContainsKey( new Uri( url ).GetLeftPart( UriPartial.Authority ) );

	/// <summary>An HTTP/1.0 response closes the connection unless it says "Connection: keep-alive".</summary>
	private static void NoteConnectionReuse( string url, HttpResponseMessage response )
	{
		bool keepAlive = response.Version >= HttpVersion.Version11
			? response.Headers.ConnectionClose != true
			: response.Headers.Connection.Contains( "keep-alive", StringComparer.OrdinalIgnoreCase );
		if( !keepAlive )
			ClosesConnections.TryAdd( new Uri( url ).GetLeftPart( UriPartial.Authority ), true );
	}

	private static System.Threading.CancellationTokenSource RequestTimeout( int timeoutMs )
		=> new( timeoutMs > 0 ? TimeSpan.FromMilliseconds( timeoutMs ) : DefaultRequestTimeout );

	public async static Task<JsonRpcResponse?> SendJsonAsync( this JsonRpcRequest request, string url, int timeout = 0, IWebProxy? proxy = null, System.Threading.CancellationToken token = default )
	{
		using var cts = RequestTimeout( timeout );
		using var link = token.Register( cts.Cancel );
		var httpClient = HttpClientFor( proxy, pooled: !ServerClosesConnections( url ) );

		// PostAsJsonAsync handles body serialization, sets Content-Type to application/json, 
		// and defaults to UTF-8 encoding automatically under the hood.
		using var response = await httpClient.PostAsJsonAsync(url, request, cts.Token);
		NoteConnectionReuse( url, response );
		response.EnsureSuccessStatusCode();

		// ReadFromJsonAsync automatically streams and deserializes the JSON response body
		JsonRpcResponse? result = await response.Content.ReadFromJsonAsync<JsonRpcResponse>(cts.Token);
		return result;
	}
	public static bool DownloadAll( this HpVersion[] versions, out List<HpVersion> failedDownloads )
	{
		failedDownloads = [];
		bool isSuccess = true;
		foreach( var version in versions ) {
			if( !version.DownloadFile() ) {
				isSuccess = false;
				failedDownloads.Add( version );
			}
		}
		return isSuccess;
	}


	public static async Task<(bool success, List<HackFile> hackFiles, List<HackResultTree.ResultNode> hackResults)> GetHackFileWithDependencies( this HackFile file )
	{
		HackResultTree hackTree = new(file);
		List<HackFile> hackFiles = [];
		List<HackResultTree.ResultNode> hackResults = [];

		//= TryFilePathToHackWithDependencies(out var list, rHack);
		// var queue = new Queue<HackResultTree.ResultNode>();
		//list.Select(r => new HackResultTree.ResultNode(r))
		// var cDict = new ConcurrentDictionary<string, ResultHackFile>();

		await Task.Run( () => hackTree.Root?.ProcessDependencies() );
		if( hackTree.Root?.IsBrokenPropagated is true ) {
			hackResults = hackTree.Root.BrokenNodes as List<HackResultTree.ResultNode> ?? [];
			return (false, hackFiles, hackResults);
		}
		hackFiles = hackTree.Root?.GetAllNodes().Select( r => r.Value?.Hack ).Where( h => h is not null ).ToList()!;
		// hackFiles = [.. hackResults.SkipSelect<HackResultTree.ResultNode, HackFile>(r => (r.Value?.Hack is null, r.Value?.Hack!))];
		return (true, hackFiles, hackResults);
	}
	public static async Task<(bool, HpVersion?)> GetVersionFromLocal( this HackFile hackFile )
	{
		// the file's folder as an hp.directory complete_name ("root / a / b"): from its RelativePath
		// ("root\a\b"), else from where it is on disk
		string filePath = hackFile.RelativePath is { Length: > 0 } relative
			? string.Join(PwaPaths.DirectorySeparator, [PwaPaths.Root, .. PwaPaths.Segments(relative)])
			: PwaPaths.ToServerDirectory(hackFile.DirectoryName ?? Path.GetDirectoryName(hackFile.FullPath)) ?? PwaPaths.Root;
		// the domain itself (a list of conditions); it was wrapped in one extra list, which Odoo
		// rejected with "unhashable type: 'list'"
		ArrayList arrList =
				[
					new ArrayList() { "name", "=", hackFile.Name },
					new ArrayList() { "directory_complete_name", "=", filePath },
				];
		// metadata only: the second positional parameter is includedFields, which made this
		// download just the file contents and preview image (and no metadata)
		HpVersion? versionModel = (await HpVersion.GetRecordsBySearchAsync(arrList, excludedFields: ["file_contents", "preview_image"]))?[0];

		return (versionModel != null, versionModel);
	}
	/// <summary>
	/// The files directly in <paramref name="folderPath"/> plus everything its SolidWorks files
	/// reference, each once. Names the server's entry filters reject (SolidWorks "~$" lock files)
	/// are left out. A SolidWorks file whose references can't all be found is left out with its
	/// tree and reported in <paramref name="broken"/>; returns false when there were any.
	/// </summary>
	public static bool GetHackFolderWithDependencies( string folderPath, out List<HackFile> hackFiles, out List<HackResultTree.ResultNode> broken )
	{
		HashSet<HackFile> files = [];
		broken = [];
		foreach( string path in Directory.EnumerateFiles( folderPath, "*", SearchOption.TopDirectoryOnly ) )
		{
			if( PdmCommitOperations.IsFilteredName( Path.GetFileName( path ) ) )
				continue;
			HackFile? hack = HackFile.GetFromPath( path );
			if( hack is not { Exists: true } )
				continue;
			if( !SwDocumentTypes.IsSolidWorks( path ) )
			{
				files.Add( hack );
				continue;
			}
			// the tree used to be kept only when it was broken, so no SolidWorks file was ever committed
			var (success, tree, results) = GetHackFileWithDependencies( hack ).GetAwaiter().GetResult();
			if( success )
				files.AddAll( tree );
			else
				broken.AddRange( results );
		}
		hackFiles = [ .. files ];
		return broken.Count == 0;
	}
	public static async Task<(bool success, List<HackFile>? hackFiles, List<HackResultTree.ResultNode> results)> GetHackFileWithDependencies( EntryRow? entry, bool listOutputDialog )
		=> await GetHackFileWithDependencies( new HackFile( entry ) );

	public static async Task<(bool success, List<HackFile>? hackFiles, List<HackResultTree.ResultNode> results)> GetHackFileWithDependencies( string filePath, bool listOutputDialog )
		=> await GetHackFileWithDependencies( new HackFile( new FileInfo( filePath ) ) );





}

public static class OdooFieldHelpers
{
	public static object? ToNativeObject( this JsonElement element )
	{
		switch( element.ValueKind ) {
			case JsonValueKind.Object:
			var dict = new Hashtable();
			foreach( JsonProperty property in element.EnumerateObject() ) {
				dict[ property.Name ] = ToNativeObject( property.Value );
			}
			return dict;

			case JsonValueKind.Array:
			var list = new ArrayList();
			foreach( JsonElement item in element.EnumerateArray() ) {
				list.Add( ToNativeObject( item ) );
			}
			return list;

			case JsonValueKind.String:
			// If it's a DateTime format you expect, you can handle it here, otherwise:
			return element.GetString();

			case JsonValueKind.Number:
			if( element.TryGetInt32( out int iVal ) )
				return iVal;
			if( element.TryGetInt64( out long lVal ) )
				return lVal;
			return element.GetDouble();

			case JsonValueKind.True:
			return true;

			case JsonValueKind.False:
			return false;

			case JsonValueKind.Null:
			case JsonValueKind.Undefined:
			return null;

			default:
			return element.GetRawText();
		}
	}
	public static bool TryCast<T>( object? value, out T? result )
	{
		if( value is T t ) {
			result = t;
			return true;
		}

		result = default;
		return false;
	}
	public static bool TryCast<TIn, TOut>(
		object? value,
		in Func<TIn, TOut> func,
		out TOut? result )
	{
		if( value is TIn t ) {
			result = func( t );
			return true;
		}
		result = default;
		return false;
	}
	public static bool TryCastAny<T1, T2, TOut>(
		object? value,
		in Func<T1, TOut> f1,
		in Func<T2, TOut> f2,
		out TOut result )
	{
		return value switch {
			T1 t1 => ReturnTrue( result = f1( t1 ) ),
			T2 t2 => ReturnTrue( result = f2( t2 ) ),
			_ => ReturnFalse( result = default! ),
		};
	}
	public static bool TryCastAny<T1, T2, T3, TOut>(
		object? value,
		in Func<T1, TOut> f1,
		in Func<T2, TOut> f2,
		in Func<T3, TOut> f3,
		out TOut result )
	{
		return value switch {
			T1 t1 => ReturnTrue( result = f1( t1 ) ),
			T2 t2 => ReturnTrue( result = f2( t2 ) ),
			T3 t3 => ReturnTrue( result = f3( t3 ) ),
			_ => ReturnFalse( result = default! ),
		};
	}
	public static bool TryCastAny<T1, T2, T3, T4, TOut>(
		object? value,
		in Func<T1, TOut> f1,
		in Func<T2, TOut> f2,
		in Func<T3, TOut> f3,
		in Func<T4, TOut> f4,
		out TOut result )
	{
		return value switch {
			T1 t1 => ReturnTrue( result = f1( t1 ) ),
			T2 t2 => ReturnTrue( result = f2( t2 ) ),
			T3 t3 => ReturnTrue( result = f3( t3 ) ),
			T4 t4 => ReturnTrue( result = f4( t4 ) ),
			_ => ReturnFalse( result = default! ),
		};
	}
	private static bool ReturnTrue<T>( in T _ ) => true;
	private static bool ReturnFalse<T>( in T _ ) => false;
	private static TOut? TryNull<TIn, TOut>( TIn input, Func<TIn, TOut> func )
	{
		try {
			return func( input );
		} catch {
			return default;
		}
	}
	public static object? Convert( OdooFieldType type, object? value )
	{
		value = Normalize( type, value );
		return value is null
			? null
			: type switch {
				// int
				OdooFieldType.Integer => ConvertInteger( value ),
				// string
				OdooFieldType.Char => ConvertString( value ),
				OdooFieldType.Text => ConvertString( value ),
				OdooFieldType.Html => ConvertString( value ),
				OdooFieldType.Selection => ConvertSelectionString( value ),
				OdooFieldType.Reference => ConvertReferenceString( value ),
				// float
				OdooFieldType.Float => ConvertFloat( value ),
				// decimal
				OdooFieldType.Monetary => ConvertMonetary( value ),
				// bool
				OdooFieldType.Boolean => ConvertBoolean( value ),
				// DateTime
				OdooFieldType.DateTime => ConvertDateTime( value ),
				// Date
				OdooFieldType.Date => ConvertDate( value ),
				// ValueTuple<int,string>?
				OdooFieldType.Many2One => ConvertMany2one( value ),
				// int[]
				OdooFieldType.One2Many => ConvertOne2many( value ),
				OdooFieldType.Many2Many => ConvertMany2many( value ),
				// Hashtable
				OdooFieldType.Json => ConvertJsonTable( value ),
				OdooFieldType.Serialized => ConvertSerializedDict( value ),
				// byte[]
				OdooFieldType.Binary => ConvertBinary( value ),
				OdooFieldType.Image => ConvertBinary( value ),
				_ => value
			};
	}

	// --------------------------------------------------------------------
	// Normalization + shape validation
	// --------------------------------------------------------------------

	private static object? Normalize( OdooFieldType type, object? value )
	{
		// Global Odoo rule: non-boolean fields use False for null
		if( value is bool b && b == false && type != OdooFieldType.Boolean )
			return null;

		return value;
	}

	private static bool IsValidShape( OdooFieldType type, object? value ) =>
		type switch {
			OdooFieldType.Char
				or OdooFieldType.Text
				or OdooFieldType.Html
				=> value is null or string,

			OdooFieldType.Integer
				=> value is null or int or long or string,

			OdooFieldType.Float
				=> value is null or double or decimal or string,

			OdooFieldType.Monetary
				=> value is null or decimal or double or string,

			OdooFieldType.Boolean
				=> value is null or bool or int or string,

			OdooFieldType.Date
				or OdooFieldType.DateTime
				=> value is null or string or DateTime,

			OdooFieldType.Binary
				=> value is null or string or byte[],

			OdooFieldType.Many2One
				=> value is null or object[],

			OdooFieldType.One2Many
				or OdooFieldType.Many2Many
				=> value is null or int[] or ArrayList,

			OdooFieldType.Selection
				=> value is null or string or int,

			OdooFieldType.Reference
				=> value is null or string or object[],

			OdooFieldType.Serialized
				=> value is null
					or string
					or Dictionary<string, object>,

			_ => true
		};

	private static int FromInt( int i ) => i;
	private static int FromLong( long l ) => ( int )l;
	private static float FromFloat( float f ) => f;
	private static float FromDouble( double d ) => ( float )d;
	private static float FromDecimal( decimal m ) => ( float )m;
	private static decimal FromDecimalMonetary( decimal m ) => m;
	private static decimal FromDoubleMonetary( double d ) => ( decimal )d;
	private static (int id, string name)? FromObjectArray( object value )
		=> value is IList list && list.Count >= 2
			? (list[ 0 ] as int? ?? 0, list[ 1 ] as string ?? "")
			: null;
	private static (int id, string name)? FromArrayList( object value )
		=> FromObjectArray( value );

	// Char/Text/Html -> string?
	public static string ConvertString( object value )
	{
		if( TryCast<string>( value, out var s ) )
			return s!;

		return value.ToString()!;
	}
	// Integer -> int
	public static int ConvertInteger( object value )
	{
		// Allowed shapes: int, long, string (validated by IsValidShape)
		if( TryCastAny<int, long, int>(
				value,
				FromInt,
				FromLong,
				out var result ) )
			return result;

		return default;
	}
	// Float -> double
	public static float ConvertFloat( object value )
	{
		// Allowed shapes: double, decimal, string
		if( TryCastAny<float, double, decimal, float>(
				value,
				FromFloat,
				FromDouble,
				FromDecimal,
				out var result ) )
			return result;

		return default;
	}
	// Monetary -> decimal
	public static decimal ConvertMonetary( object value )
	{
		// Allowed shapes: decimal, double, string
		if( TryCastAny<decimal, double, decimal>(
				value,
				FromDecimalMonetary,
				FromDoubleMonetary,
				out var result ) )
			return result;

		return default;
	}
	// Boolean -> bool
	public static bool ConvertBoolean( object value )
		=> TryCast<bool>( value, out var b ) && b;
	// Date -> DateTime (date-only)
	public static DateTime ConvertDate( object value )
		=> ConvertDateTime( value ).Date;
	// DateTime -> DateTime
	public static DateTime ConvertDateTime( object value )
	{
		if( value is DateTime dt )
			return dt;

		if( TryCast<string>( value, out var s ) &&
			DateTime.TryParse( s, out var parsed ) )
			return parsed;

		return default;
	}
	// Binary -> byte[]?
	public static byte[] ConvertBinary( object value )
	{
		return TryCast<string>( value, out var s ) && !string.IsNullOrEmpty( s )
			? TryNull( s, System.Convert.FromBase64String )!
			: TryCast<byte[]>( value, out var bytes )
				? bytes!
				: default!;
	}
	// Many2one -> (int id, string name)?
	// Odoo shape: [id, "Name"]
	public static Many2One ConvertMany2one( object value )
	{
		if( !TryCast<object, ValueTuple<int, string>?>(
				value,
				FromObjectArray,
				out var arr ) )
			return new Many2One { id = 0, name = "" };

		var id = ConvertInteger(arr?.Item1 ?? 0);
		var name = ConvertString(arr?.Item2 ?? "") ?? string.Empty;

		return new Many2One { id = id, name = name };
		;
	}
	// One2many/Many2many -> int[]
	public static One2Many ConvertOne2many( object value )
	{
		if( TryCast<int[]>( value, out var ids ) )
			return ids!;

		if( TryCast<IList>( value, out var al ) )
			return new One2Many { Ids = [ .. al?.Cast<int>() ?? [] ] };

		return [];
	}
	public static Many2Many ConvertMany2many( object value )
	{
		if( TryCast<int[]>( value, out var ids ) )
			return ids!;

		if( TryCast<IList>( value, out var al ) )
			return [ .. al?.Cast<int>() ?? [] ];

		return [];
	}
	// Selection -> preserve as string or int (you can refine as needed)
	public static string ConvertSelectionString( object value )
		=> value is string s ? s : value.ToString()!;
	// Reference -> typically "model_name,ID" or [model, id]
	public static string ConvertReferenceString( object value )
		=> ConvertString( value );
	// Serialized -> leave as-is, or decode JSON
	public static string ConvertSerializedString( object value )
		=> value is string s ? s : string.Empty;

	public static Hashtable ConvertJsonTable( object value )
	{
		// the JSON-RPC reader already turns objects into Hashtables; only a string still needs parsing
		if( value is Hashtable table )
			return table;
		Hashtable result = [];
		var valueDict = ConvertSerializedDict(value);
		foreach( var item in valueDict ) {
			result[ item.Key ] = item.Value;
		}
		return result;
	}
	public static Dictionary<string, object> ConvertSerializedDict( object value )
	{
		if( value is IDictionary parsed )
		{
			Dictionary<string, object> copy = [];
			foreach( DictionaryEntry entry in parsed )
				copy[ entry.Key.ToString() ?? "" ] = entry.Value!;
			return copy;
		}
		return JsonConvert.DeserializeObject<Dictionary<string, object>>( ConvertSerializedString( value ) ) ?? [];
	}
}