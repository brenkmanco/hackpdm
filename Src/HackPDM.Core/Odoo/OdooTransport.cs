using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace HackPDM.Core.Odoo;

/// <summary>HTTP transport for Odoo JSON-RPC with connection pooling and HTTP/1.0 Werkzeug fallback.</summary>
public static class OdooTransport
{
	private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds( 100 );
	private static readonly ConcurrentDictionary<(IWebProxy? Proxy, bool Pooled), Lazy<HttpClient>> HttpClients = new();
	private static readonly ConcurrentDictionary<string, bool> ClosesConnections = new( StringComparer.OrdinalIgnoreCase );

	private static HttpClient CreateHttpClient( IWebProxy? proxy, bool pooled )
	{
		SocketsHttpHandler handler = new()
		{
			AutomaticDecompression = DecompressionMethods.All,
			PooledConnectionLifetime = pooled ? TimeSpan.FromMinutes( 5 ) : TimeSpan.Zero,
			ConnectTimeout = TimeSpan.FromSeconds( 15 ),
		};
		if( proxy is not null )
		{
			handler.Proxy = proxy;
			handler.UseProxy = true;
		}
		return new HttpClient( handler ) { Timeout = Timeout.InfiniteTimeSpan };
	}

	private static HttpClient HttpClientFor( IWebProxy? proxy, bool pooled )
		=> HttpClients.GetOrAdd(
			( proxy, pooled ),
			static key => new Lazy<HttpClient>( () => CreateHttpClient( key.Proxy, key.Pooled ), LazyThreadSafetyMode.ExecutionAndPublication ) ).Value;

	internal static bool ServerClosesConnections( string url )
		=> !ClosesConnections.IsEmpty && ClosesConnections.ContainsKey( new Uri( url ).GetLeftPart( UriPartial.Authority ) );

	private static void NoteConnectionReuse( string url, HttpResponseMessage response )
	{
		bool keepAlive = response.Version >= HttpVersion.Version11
			? response.Headers.ConnectionClose != true
			: response.Headers.Connection.Contains( "keep-alive", StringComparer.OrdinalIgnoreCase );
		if( !keepAlive )
			ClosesConnections.TryAdd( new Uri( url ).GetLeftPart( UriPartial.Authority ), true );
	}

	private static CancellationTokenSource RequestTimeout( int timeoutMs )
		=> new( timeoutMs > 0 ? TimeSpan.FromMilliseconds( timeoutMs ) : DefaultRequestTimeout );

	public static async Task<JsonRpcResponse?> SendJsonAsync(
		this JsonRpcRequest request,
		string url,
		int timeout = 0,
		IWebProxy? proxy = null,
		CancellationToken token = default )
	{
		using CancellationTokenSource cts = RequestTimeout( timeout );
		using CancellationTokenRegistration link = token.CanBeCanceled
			? token.UnsafeRegister( static state => ( (CancellationTokenSource)state! ).Cancel(), cts )
			: default;
		HttpClient httpClient = HttpClientFor( proxy, pooled: !ServerClosesConnections( url ) );

		using ByteArrayContent body = SerializeRequest( request );
		using HttpRequestMessage message = new( HttpMethod.Post, url )
		{
			Content = body,
		};
		using HttpResponseMessage response = await httpClient.SendAsync(
			message,
			HttpCompletionOption.ResponseHeadersRead,
			cts.Token ).ConfigureAwait( false );
		NoteConnectionReuse( url, response );
		response.EnsureSuccessStatusCode();
		Stream stream = await response.Content.ReadAsStreamAsync( cts.Token ).ConfigureAwait( false );
		await using( stream.ConfigureAwait( false ) )
		{
			using JsonDocument doc = await JsonDocument.ParseAsync( stream, cancellationToken: cts.Token ).ConfigureAwait( false );
			return ParseResponse( doc.RootElement );
		}
	}

	internal static ByteArrayContent SerializeRequest( JsonRpcRequest request )
	{
		ArrayBufferWriter<byte> buffer = new( 1024 );
		using( Utf8JsonWriter writer = new( buffer ) )
		{
			writer.WriteStartObject();
			writer.WriteString( "jsonrpc", request.Version );
			writer.WriteString( "method", request.Method );
			writer.WritePropertyName( "params" );
			writer.WriteStartObject();
			if( request.Params.Service is null )
				writer.WriteNull( "service" );
			else
				writer.WriteString( "service", request.Params.Service );
			if( request.Params.Method is null )
				writer.WriteNull( "method" );
			else
				writer.WriteString( "method", request.Params.Method );
			writer.WritePropertyName( "args" );
			if( request.Params.Args is null )
			{
				writer.WriteNullValue();
			}
			else
			{
				writer.WriteStartArray();
				for( int i = 0; i < request.Params.Args.Count; i++ )
					WriteRpcValue( writer, request.Params.Args[ i ] );
				writer.WriteEndArray();
			}
			writer.WriteEndObject();
			writer.WriteEndObject();
		}

		ByteArrayContent content = new( buffer.WrittenSpan.ToArray() );
		content.Headers.ContentType = new MediaTypeHeaderValue( "application/json" ) { CharSet = "utf-8" };
		return content;
	}

	private static void WriteRpcValue( Utf8JsonWriter writer, object? value )
	{
		switch( value )
		{
			case null:
				writer.WriteNullValue();
				break;
			case bool b:
				writer.WriteBooleanValue( b );
				break;
			case string s:
				writer.WriteStringValue( s );
				break;
			case int i:
				writer.WriteNumberValue( i );
				break;
			case long l:
				writer.WriteNumberValue( l );
				break;
			case float f:
				writer.WriteNumberValue( f );
				break;
			case double d:
				writer.WriteNumberValue( d );
				break;
			case decimal m:
				writer.WriteNumberValue( m );
				break;
			case uint u:
				writer.WriteNumberValue( u );
				break;
			case ulong ul:
				writer.WriteNumberValue( ul );
				break;
			case short sh:
				writer.WriteNumberValue( sh );
				break;
			case byte by:
				writer.WriteNumberValue( by );
				break;
			case byte[] bytes:
				writer.WriteBase64StringValue( bytes );
				break;
			case DateTime dt:
				writer.WriteStringValue( OdooValues.OutDateTime( dt ) );
				break;
			case DateTimeOffset dto:
				writer.WriteStringValue( dto );
				break;
			case Guid g:
				writer.WriteStringValue( g );
				break;
			case JsonElement je:
				je.WriteTo( writer );
				break;
			case JsonDocument jd:
				jd.RootElement.WriteTo( writer );
				break;
			case OdooDomain domain:
				WriteRpcValue( writer, domain.ToRpc() );
				break;
			case DomainTerm term:
				WriteRpcValue( writer, term.ToRpc() );
				break;
			case IReadOnlyDictionary<string, object?> roDict:
				writer.WriteStartObject();
				foreach( var (k, v) in roDict )
				{
					writer.WritePropertyName( k );
					WriteRpcValue( writer, v );
				}
				writer.WriteEndObject();
				break;
			case IDictionary dict:
				writer.WriteStartObject();
				foreach( DictionaryEntry entry in dict )
				{
					writer.WritePropertyName( entry.Key?.ToString() ?? "" );
					WriteRpcValue( writer, entry.Value );
				}
				writer.WriteEndObject();
				break;
			case IEnumerable items:
				writer.WriteStartArray();
				foreach( object? item in items )
					WriteRpcValue( writer, item );
				writer.WriteEndArray();
				break;
			default:
				writer.WriteStringValue( value.ToString() );
				break;
		}
	}

	internal static JsonRpcResponse? ParseResponse( JsonElement root )
	{
		if( root.ValueKind == JsonValueKind.Null )
			return null;
		if( root.ValueKind != JsonValueKind.Object )
			throw new JsonException( $"Expected JSON-RPC response object, got {root.ValueKind}." );

		JsonRpcResponse response = new();
		if( root.TryGetProperty( "jsonrpc", out JsonElement ver ) && ver.ValueKind == JsonValueKind.String )
			response.Version = ver.GetString();
		if( root.TryGetProperty( "id", out JsonElement id ) )
		{
			response.ID = id.ValueKind switch
			{
				JsonValueKind.String => id.GetString(),
				JsonValueKind.Null or JsonValueKind.Undefined => null,
				_ => id.GetRawText(),
			};
		}
		if( root.TryGetProperty( "result", out JsonElement res ) )
			response.Result = res.Clone();
		if( root.TryGetProperty( "error", out JsonElement err ) && err.ValueKind == JsonValueKind.Object )
			response.Error = ParseError( err );
		return response;
	}

	private static JsonRpcError ParseError( JsonElement err )
	{
		JsonRpcError error = new();
		if( err.TryGetProperty( "code", out JsonElement code ) )
			error.Code = code.Clone();
		if( err.TryGetProperty( "message", out JsonElement msg ) && msg.ValueKind == JsonValueKind.String )
			error.Message = msg.GetString();
		if( err.TryGetProperty( "data", out JsonElement data ) && data.ValueKind == JsonValueKind.Object )
			error.Data = ParseErrorData( data );
		return error;
	}

	private static JsonRpcData ParseErrorData( JsonElement data )
	{
		JsonRpcData parsed = new();
		if( data.TryGetProperty( "name", out JsonElement name ) && name.ValueKind == JsonValueKind.String )
			parsed.Name = name.GetString();
		if( data.TryGetProperty( "debug", out JsonElement debug ) && debug.ValueKind == JsonValueKind.String )
			parsed.Debug = debug.GetString();
		if( data.TryGetProperty( "message", out JsonElement msg ) && msg.ValueKind == JsonValueKind.String )
			parsed.Message = msg.GetString();
		if( data.TryGetProperty( "arguments", out JsonElement args ) && args.ValueKind == JsonValueKind.Array )
		{
			List<object?> list = new( args.GetArrayLength() );
			foreach( JsonElement item in args.EnumerateArray() )
				list.Add( OdooValues.FromJson( item ) );
			parsed.Args = list;
		}
		if( data.TryGetProperty( "context", out JsonElement ctx ) && ctx.ValueKind == JsonValueKind.Object )
		{
			Dictionary<string, JsonElement> map = [];
			foreach( JsonProperty prop in ctx.EnumerateObject() )
				map[ prop.Name ] = prop.Value.Clone();
			parsed.Context = map;
		}
		return parsed;
	}
}
