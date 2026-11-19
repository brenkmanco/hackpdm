using System.Net;
using System.Text.Json;

using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo.Models;

namespace HackPDM.Core.Odoo;

/// <summary>Typed Odoo JSON-RPC client with idempotent transport retries.</summary>
public static class OdooRpc
{
	public const string JsonRpcEndpoint = "/jsonrpc";

	public static int CommonTimeout { get; set; } = 3000;
	public static int ObjectTimeout { get; set; } = 5000;
	public const int LongTimeout = 10000;

	private static OdooDefaults Settings => OdooDefaults.Instance;

	private static readonly HashSet<string> IdempotentObjectMethods = new( StringComparer.Ordinal )
	{
		"search", "search_read", "read", "search_count", "smart_read", "related_browse",
		"get_entries", "get_all_entry_ids", "get_children_directories_by_id",
		"get_recursive_dependency_versions", "get_recursive_dependency_entries",
		"get_recursive_dependency_entries_by_entries",
		"read_contents", "upload_chunk", "has_group",
	};

	private static bool IsIdempotentCall( string service, string method, IReadOnlyList<object?> args )
	{
		if( service == "common" && method == "version" )
			return true;
		if( service == "object" && method == "execute" && args.Count >= 5 && args[ 4 ] is string modelMethod )
			return IdempotentObjectMethods.Contains( modelMethod );
		return false;
	}

	private static bool IsTransientTransportError( Exception e )
		=> e is HttpRequestException http
			&& http.StatusCode is null
				or HttpStatusCode.BadGateway
				or HttpStatusCode.ServiceUnavailable
				or HttpStatusCode.GatewayTimeout
				or HttpStatusCode.RequestTimeout;

	public static async Task<Result<JsonElement>> CallAsync(
		string service,
		string method,
		IReadOnlyList<object?> args,
		int? timeoutMs = null,
		CancellationToken token = default )
	{
		if( OdooEndpoint.InsecureReason( Settings.OdooUrl, allowPlainHttp: !Settings.OdooUseHttps ) is { } insecure )
			return Result<JsonElement>.Fail( insecure );

		JsonRpcRequest request = new()
		{
			Params = new()
			{
				Service = service,
				Method = method,
				Args = [ .. args.Select( OdooValues.ToRpc ) ],
			},
		};

		int maxAttempts = IsIdempotentCall( service, method, args ) ? 3 : 1;
		for( int attempt = 1; attempt <= maxAttempts; attempt++ )
		{
			try
			{
				JsonRpcResponse? response = await request.SendJsonAsync(
					Settings.OdooUrl + JsonRpcEndpoint,
					timeoutMs ?? ObjectTimeout,
					token: token );
				if( response is null )
					return Result<JsonElement>.Fail( "no response from the server" );
				if( response.Error is not null )
					return Result<JsonElement>.Fail( response.Error.Describe() );
				return Result<JsonElement>.Success( response.Result?.Clone() ?? default );
			}
			catch( OperationCanceledException ) when( token.IsCancellationRequested )
			{
				throw;
			}
			catch( Exception e ) when( attempt < maxAttempts && IsTransientTransportError( e ) )
			{
				int delayMs = attempt == 1 ? 250 : 750;
				HackLogger.Warn( "OdooRpc", $"Transient RPC error on {service}.{method} (attempt {attempt}/{maxAttempts}), retrying in {delayMs}ms: {Describe( e )}" );
				await Task.Delay( delayMs, token );
			}
			catch( Exception e )
			{
				string detail = Describe( e );
				HackLogger.Error( "OdooRpc", $"RPC transport error on {service}.{method}: {detail}", e );
				return Result<JsonElement>.Fail( detail );
			}
		}
		return Result<JsonElement>.Fail( "no response from the server" );
	}

	public static async Task<Result<T>> ExecuteAsync<T>(
		string model,
		string method,
		IReadOnlyList<object?> args,
		int? timeoutMs = null,
		CancellationToken token = default )
	{
		Result<JsonElement> raw = await CallAsync(
			"object",
			"execute",
			[ Settings.OdooDb, Settings.OdooId, Settings.OdooPass, model, method, .. args ],
			timeoutMs,
			token );
		return raw.Ok ? Convert<T>( raw.Value ) : Result<T>.Fail( raw.Error );
	}

	public static async Task<Result<int>> LoginAsync( int? timeoutMs = null, CancellationToken token = default )
	{
		if( string.IsNullOrEmpty( Settings.OdooDb )
			|| string.IsNullOrEmpty( Settings.OdooUser )
			|| string.IsNullOrEmpty( Settings.OdooPass ) )
			return Result<int>.Fail( "the database, user name and password must be set" );

		Result<JsonElement> raw = await CallAsync(
			"common",
			"login",
			[ Settings.OdooDb, Settings.OdooUser, Settings.OdooPass ],
			timeoutMs ?? CommonTimeout,
			token );
		if( !raw.Ok )
			return Result<int>.Fail( raw.Error );

		int uid = raw.Value.ValueKind switch
		{
			JsonValueKind.Number => raw.Value.GetInt32(),
			JsonValueKind.String when int.TryParse( raw.Value.GetString(), out int parsed ) => parsed,
			_ => 0,
		};
		if( uid <= 0 )
			return Result<int>.Fail( "login username or password failed" );

		Settings.OdooId = uid;
		return Result<int>.Success( uid );
	}

	internal static string Describe( Exception exc )
	{
		List<string> parts = [];
		for( Exception? e = exc; e is not null; e = e.InnerException )
		{
			if( parts.Count == 0 || parts[ ^1 ] != e.Message )
				parts.Add( e.Message );
		}
		return string.Join( " -> ", parts );
	}

	public static Task<Result<IReadOnlyList<int>>> SearchAsync(
		string model,
		OdooDomain? domain = null,
		int? limit = null,
		int? timeoutMs = null,
		CancellationToken token = default )
		=> ExecuteAsync<IReadOnlyList<int>>(
			model,
			"search",
			limit is null ? [ domain ?? OdooDomain.All ] : [ domain ?? OdooDomain.All, 0, limit ],
			timeoutMs,
			token );

	public static Task<Result<int>> SearchCountAsync(
		string model,
		OdooDomain? domain = null,
		int? timeoutMs = null,
		CancellationToken token = default )
		=> ExecuteAsync<int>( model, "search_count", [ domain ?? OdooDomain.All ], timeoutMs, token );

	public static Task<Result<IReadOnlyList<OdooRow>>> ReadAsync(
		string model,
		IReadOnlyList<int> ids,
		IReadOnlyList<string>? fields = null,
		int? timeoutMs = null,
		CancellationToken token = default )
		=> ExecuteAsync<IReadOnlyList<OdooRow>>( model, "read", [ ids, fields ?? [] ], timeoutMs, token );

	public static Task<Result<IReadOnlyList<OdooRow>>> SearchReadAsync(
		string model,
		OdooDomain? domain = null,
		IReadOnlyList<string>? fields = null,
		int? limit = null,
		int? timeoutMs = null,
		CancellationToken token = default )
		=> ExecuteAsync<IReadOnlyList<OdooRow>>(
			model,
			"search_read",
			limit is null
				? [ domain ?? OdooDomain.All, fields ?? [] ]
				: [ domain ?? OdooDomain.All, fields ?? [], 0, limit ],
			timeoutMs,
			token );

	public static Task<Result<int>> CreateAsync(
		string model,
		IReadOnlyDictionary<string, object?> values,
		int? timeoutMs = null,
		CancellationToken token = default )
		=> ExecuteAsync<int>( model, "create", [ values ], timeoutMs, token );

	public static Task<Result<IReadOnlyList<int>>> CreateManyAsync(
		string model,
		IEnumerable<IReadOnlyDictionary<string, object?>> values,
		int? timeoutMs = null,
		CancellationToken token = default )
		=> ExecuteAsync<IReadOnlyList<int>>( model, "create", [ values.ToList() ], timeoutMs, token );

	public static Task<Result<bool>> WriteAsync(
		string model,
		IReadOnlyList<int> ids,
		IReadOnlyDictionary<string, object?> values,
		int? timeoutMs = null,
		CancellationToken token = default )
		=> ExecuteAsync<bool>( model, "write", [ ids, values ], timeoutMs, token );

	public static Task<Result<bool>> UnlinkAsync(
		string model,
		IReadOnlyList<int> ids,
		int? timeoutMs = null,
		CancellationToken token = default )
		=> ids.Count == 0
			? Task.FromResult( Result<bool>.Success( true ) )
			: ExecuteAsync<bool>( model, "unlink", [ ids ], timeoutMs, token );

	public static Task<Result<int>> CreateCommitAsync( int? timeoutMs = null )
		=> CreateAsync(
			HpPDMCommit.ModelName,
			new Dictionary<string, object?> { [ "node_by" ] = Settings.MyNode?.id ?? 0 },
			timeoutMs );

	internal static Result<T> Convert<T>( JsonElement json )
	{
		try
		{
			object? value = typeof( T ) switch
			{
				var t when t == typeof( JsonElement ) => json,
				var t when t == typeof( bool ) => json.ValueKind == JsonValueKind.True,
				var t when t == typeof( int ) => json.ValueKind == JsonValueKind.Number ? json.GetInt32() : 0,
				var t when t == typeof( IReadOnlyList<int> ) => OdooValues.Ids( OdooValues.FromJson( json ) ),
				var t when t == typeof( IReadOnlyList<OdooRow> ) => Rows( json ),
				var t when t == typeof( OdooRow ) => json.ValueKind == JsonValueKind.Object ? Row( json ) : null,
				var t when t == typeof( object ) => OdooValues.FromJson( json ),
				_ => json.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined or JsonValueKind.False
					? null
					: json.Deserialize<T>(),
			};
			return Result<T>.Success( (T)value! );
		}
		catch( Exception e ) when( e is JsonException or InvalidOperationException or FormatException or InvalidCastException )
		{
			return Result<T>.Fail( $"unexpected result for {typeof( T ).Name}: {e.Message}" );
		}
	}

	private static IReadOnlyList<OdooRow> Rows( JsonElement json )
		=> json.ValueKind == JsonValueKind.Array
			? [ .. json.EnumerateArray().Where( e => e.ValueKind == JsonValueKind.Object ).Select( Row ) ]
			: [];

	private static OdooRow Row( JsonElement json )
	{
		OdooRow row = [];
		foreach( JsonProperty property in json.EnumerateObject() )
			row[ property.Name ] = OdooValues.FromJson( property.Value );
		return row;
	}
}
