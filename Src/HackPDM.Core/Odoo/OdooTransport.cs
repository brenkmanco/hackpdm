using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;

namespace HackPDM.Core.Odoo;

/// <summary>HTTP transport for Odoo JSON-RPC with connection pooling and HTTP/1.0 Werkzeug fallback.</summary>
public static class OdooTransport
{
	private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds( 100 );
	private static readonly ConcurrentDictionary<(IWebProxy? Proxy, bool Pooled), HttpClient> HttpClients = new();
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
		=> HttpClients.GetOrAdd( ( proxy, pooled ), key => CreateHttpClient( key.Proxy, key.Pooled ) );

	internal static bool ServerClosesConnections( string url )
		=> ClosesConnections.ContainsKey( new Uri( url ).GetLeftPart( UriPartial.Authority ) );

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
		using CancellationTokenRegistration link = token.Register( cts.Cancel );
		HttpClient httpClient = HttpClientFor( proxy, pooled: !ServerClosesConnections( url ) );

		using HttpResponseMessage response = await httpClient.PostAsJsonAsync( url, request, cts.Token );
		NoteConnectionReuse( url, response );
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<JsonRpcResponse>( cts.Token );
	}
}
