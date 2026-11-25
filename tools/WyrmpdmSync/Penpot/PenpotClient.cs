using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WyrmpdmSync.Penpot;

/// <summary>
/// Penpot's RPC API (https://design.penpot.app/api/main/doc): every method is
/// <c>POST {base}/api/main/methods/{name}</c> with a JSON body, authenticated with a personal access token.
/// </summary>
internal sealed class PenpotClient : IDisposable
{
	private readonly HttpClient _http;

	public PenpotClient( string baseUrl, string accessToken, HttpMessageHandler? handler = null )
	{
		_http = handler is null ? new HttpClient() : new HttpClient( handler );
		_http.BaseAddress = new Uri( baseUrl.TrimEnd( '/' ) + "/api/main/methods/" );
		_http.Timeout = TimeSpan.FromSeconds( 60 );
		_http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "Token", accessToken );
		_http.DefaultRequestHeaders.UserAgent.ParseAdd( "wyrmpdm-sync/1" );
	}

	/// <summary>
	/// The access token: the PENPOT_TOKEN environment variable, else design/.penpot-token (git-ignored).
	/// Never printed or logged.
	/// </summary>
	public static string ReadAccessToken( RepoPaths paths )
	{
		string? token = Environment.GetEnvironmentVariable( "PENPOT_TOKEN" );
		if( string.IsNullOrWhiteSpace( token ) && File.Exists( paths.TokenFile ) )
			token = File.ReadAllText( paths.TokenFile );
		if( string.IsNullOrWhiteSpace( token ) )
			throw new SyncException(
				"No Penpot access token. Create one in Penpot (Your account > Settings > Access tokens) and save it in " +
				$"{paths.Relative( paths.TokenFile )} or the PENPOT_TOKEN environment variable." );
		return token.Trim();
	}

	/// <summary>Calls an RPC method and returns its JSON result.</summary>
	public async Task<JsonNode?> CallAsync( string method, object body, CancellationToken ct = default )
	{
		string text = await SendAsync( method, JsonSerializer.Serialize( body ), "application/json", "application/json", ct );
		return string.IsNullOrWhiteSpace( text ) ? null : JsonNode.Parse( text );
	}

	/// <summary>
	/// Calls an RPC method asking for Penpot's native Transit encoding, decoded to plain JSON with Penpot's own
	/// (kebab-case) keys. Unlike the JSON encoding it keeps every id, including the token library's.
	/// </summary>
	public async Task<JsonNode?> CallTransitAsync( string method, object body, CancellationToken ct = default )
	{
		string text = await SendAsync( method, JsonSerializer.Serialize( body ), "application/json", "application/transit+json", ct );
		return string.IsNullOrWhiteSpace( text ) ? null : Transit.Decode( text );
	}

	private async Task<string> SendAsync( string method, string body, string contentType, string accept, CancellationToken ct )
	{
		using HttpRequestMessage request = new( HttpMethod.Post, method )
		{
			Content = new StringContent( body, Encoding.UTF8, contentType ),
		};
		request.Headers.Accept.Clear();
		request.Headers.Accept.Add( new MediaTypeWithQualityHeaderValue( accept ) );
		using HttpResponseMessage response = await _http.SendAsync( request, ct );
		string text = await response.Content.ReadAsStringAsync( ct );
		if( !response.IsSuccessStatusCode )
			throw new SyncException( Describe( method, response.StatusCode, text ) );
		return text;
	}

	/// <summary>The signed-in profile: a cheap way to check the token.</summary>
	public Task<JsonNode?> GetProfileAsync( CancellationToken ct = default ) => CallAsync( "get-profile", new { }, ct );

	/// <summary>A whole file, with its data (pages, shapes, components, colors, tokens), read through Transit so every id is kept.</summary>
	public async Task<JsonObject> GetFileAsync( Guid fileId, CancellationToken ct = default )
		=> await CallTransitAsync( "get-file", new { id = fileId }, ct ) as JsonObject
		   ?? throw new SyncException( $"Penpot returned no file for {fileId}." );

	/// <summary>
	/// Applies <paramref name="changes"/> to the file. <c>revn</c> and <c>vern</c> are the revision just read: Penpot
	/// uses them to merge with edits made in the meantime. Sent as Transit (see <see cref="TransitWriter"/>) because
	/// shape values such as stroke styles must arrive as keywords. Penpot applies all the changes or none.
	/// </summary>
	public async Task UpdateFileAsync( PenpotFile file, IEnumerable<JsonObject> changes, CancellationToken ct = default )
	{
		JsonObject body = new()
		{
			[ "id" ] = file.Id.ToString(),
			[ "session-id" ] = Guid.NewGuid().ToString(),
			[ "revn" ] = file.Revn,
			[ "vern" ] = file.Vern,
			[ "features" ] = new JsonArray( [ .. file.Features.Select( f => (JsonNode)JsonValue.Create( f )! ) ] ),
			[ "changes" ] = new JsonArray( [ .. changes.Select( c => c.DeepClone() ) ] ),
		};
		await SendAsync( "update-file", TransitWriter.Encode( body ), "application/transit+json", "application/json", ct );
	}

	/// <summary>A piece of a large file stored apart from its main data (Penpot's "pointer map" feature).</summary>
	public async Task<JsonNode?> GetFileFragmentAsync( Guid fileId, Guid fragmentId, CancellationToken ct = default )
		=> ( await CallAsync( "get-file-fragment", new { fileId, fragmentId }, ct ) )?[ "content" ];

	private static string Describe( string method, HttpStatusCode status, string body )
	{
		string detail = body.Length > 400 ? body[ ..400 ] + "…" : body;
		return status switch
		{
			HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
				$"Penpot refused {method} ({(int)status}): the access token is missing, expired or for another Penpot server.",
			HttpStatusCode.NotFound => $"Penpot has no such object for {method} (404): {detail}",
			_ => $"Penpot {method} failed ({(int)status}): {detail}",
		};
	}

	public void Dispose() => _http.Dispose();
}
