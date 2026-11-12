using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Infrastructure.RPC;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>Sends JSON-RPC requests over HTTP: connection pooling, per-call timeouts.</summary>
public static class OdooTransport
{
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
		return new HttpClient( handler ) { Timeout = Timeout.InfiniteTimeSpan };
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

	private static CancellationTokenSource RequestTimeout( int timeoutMs )
		=> new( timeoutMs > 0 ? TimeSpan.FromMilliseconds( timeoutMs ) : DefaultRequestTimeout );

	/// <summary>POSTs a JSON-RPC request to <paramref name="url"/> and deserializes the response body.</summary>
	/// <param name="request">The request to serialize as the JSON body.</param>
	/// <param name="url">The full endpoint URL, for example the Odoo base URL plus <c>/jsonrpc</c>.</param>
	/// <param name="timeout">Timeout for the whole request (send and read) in milliseconds; 0 or less means 100 seconds.</param>
	/// <param name="proxy">An optional HTTP proxy. Each distinct proxy gets its own cached <see cref="HttpClient"/>.</param>
	/// <param name="token">Cancels the request.</param>
	/// <returns>The deserialized response, or null when the body is JSON <c>null</c>. A JSON-RPC error is returned in <see cref="JsonRpcResponse.Error"/>, not thrown.</returns>
	/// <remarks>
	/// Connections are pooled and reused, except for servers that have answered HTTP/1.0 without keep-alive
	/// (Odoo's built-in Werkzeug server): once seen, later calls to that host use a client that never reuses
	/// connections, to avoid "response ended prematurely" failures on a socket the server already closed.
	/// Connecting times out after 15 seconds regardless of <paramref name="timeout"/>.
	/// </remarks>
	/// <exception cref="OperationCanceledException">The timeout elapsed or <paramref name="token"/> was cancelled (thrown as <see cref="TaskCanceledException"/>).</exception>
	/// <exception cref="HttpRequestException">The connection failed or the server returned a non-success status code.</exception>
	/// <exception cref="System.Text.Json.JsonException">The response body is not valid JSON-RPC JSON.</exception>
	/// <exception cref="UriFormatException"><paramref name="url"/> is not an absolute URI.</exception>
	public async static Task<JsonRpcResponse?> SendJsonAsync( this JsonRpcRequest request, string url, int timeout = 0, IWebProxy? proxy = null, CancellationToken token = default )
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
}
