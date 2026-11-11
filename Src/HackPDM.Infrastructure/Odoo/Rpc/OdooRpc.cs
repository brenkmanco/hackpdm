using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using HackPDM.Infrastructure.RPC;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>A row returned by read / search_read: field name to value (see <see cref="OdooValues"/>).</summary>
public sealed class OdooRow : Dictionary<string, object?>
{
	public OdooRow() { }
	public OdooRow( IDictionary<string, object?> values ) : base( values ) { }

	public int Id => this.Int( "id" ) ?? 0;
}

/// <summary>
/// The Odoo JSON-RPC client: one transport (<see cref="CallAsync"/>) and the typed verbs on top of it.
/// Every call returns a <see cref="Result{T}"/> with the server's (or the transport's) error instead of
/// setting a shared static.
/// </summary>
public static class OdooRpc
{
	public const string JsonRpcEndpoint = "/jsonrpc";

	/// <summary>Timeout for the common service (login), in milliseconds.</summary>
	public static int CommonTimeout { get; set; } = 3000;
	/// <summary>Default timeout for model calls, in milliseconds.</summary>
	public static int ObjectTimeout { get; set; } = 5000;
	/// <summary>Timeout for reads that can return many rows, in milliseconds.</summary>
	public const int LongTimeout = 10000;

	private static HackPDM.Domain.OdooModels.IOdooDefaults Settings => OdooDefaults.Instance;

	// ------------------------------------------------------------------ transport

	/// <summary>The one transport: calls <paramref name="method"/> of an Odoo service and returns its raw result.</summary>
	public static async Task<Result<JsonElement>> CallAsync( string service, string method, IReadOnlyList<object?> args, int? timeoutMs = null, CancellationToken token = default )
	{
		JsonRpcRequest request = new()
		{
			Params = new()
			{
				Service = service,
				Method = method,
				Args = [ .. args.Select( OdooValues.ToRpc ) ],
			},
		};
		try
		{
			var response = await request.SendJsonAsync( Settings.OdooUrl + JsonRpcEndpoint, timeoutMs ?? ObjectTimeout, token: token );
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
		catch( Exception e )
		{
			return Result<JsonElement>.Fail( OdooClient.Describe( e ) );
		}
	}

	/// <summary>Calls a model method (<c>object.execute</c>) with the logged-in user and converts the result.</summary>
	public static async Task<Result<T>> ExecuteAsync<T>( string model, string method, IReadOnlyList<object?> args, int? timeoutMs = null, CancellationToken token = default )
	{
		Result<JsonElement> raw = await CallAsync( "object", "execute",
			[ Settings.OdooDb, Settings.OdooId, Settings.OdooPass, model, method, .. args ], timeoutMs, token );
		return raw.Ok ? Convert<T>( raw.Value ) : Result<T>.Fail( raw.Error! );
	}

	/// <summary>Logs in; the user id (0 with an error for wrong credentials).</summary>
	public static async Task<Result<int>> LoginAsync( int? timeoutMs = null, CancellationToken token = default )
	{
		if( string.IsNullOrEmpty( Settings?.OdooDb ) || string.IsNullOrEmpty( Settings.OdooUser ) || string.IsNullOrEmpty( Settings.OdooPass ) )
			return Result<int>.Fail( "the database, user name and password must be set" );
		Result<JsonElement> raw = await CallAsync( "common", "login", [ Settings.OdooDb, Settings.OdooUser, Settings.OdooPass ], timeoutMs ?? CommonTimeout, token );
		if( !raw.Ok )
			return Result<int>.Fail( raw.Error! );
		int uid = raw.Value.ValueKind switch
		{
			JsonValueKind.Number => raw.Value.GetInt32(),
			JsonValueKind.String when int.TryParse( raw.Value.GetString(), out int parsed ) => parsed,
			_ => 0,
		};
		return uid > 0 ? Result<int>.Success( uid ) : Result<int>.Fail( "login username or password failed" );
	}

	// ------------------------------------------------------------------ typed verbs

	public static Task<Result<IReadOnlyList<int>>> SearchAsync( string model, Domain? domain = null, int? limit = null, int? timeoutMs = null, CancellationToken token = default )
		=> ExecuteAsync<IReadOnlyList<int>>( model, "search", limit is null ? [ domain ?? Domain.All ] : [ domain ?? Domain.All, 0, limit ], timeoutMs, token );

	public static Task<Result<int>> SearchCountAsync( string model, Domain? domain = null, int? timeoutMs = null, CancellationToken token = default )
		=> ExecuteAsync<int>( model, "search_count", [ domain ?? Domain.All ], timeoutMs, token );

	public static Task<Result<IReadOnlyList<OdooRow>>> ReadAsync( string model, IReadOnlyList<int> ids, IReadOnlyList<string>? fields = null, int? timeoutMs = null, CancellationToken token = default )
		=> ExecuteAsync<IReadOnlyList<OdooRow>>( model, "read", [ ids, fields ?? [] ], timeoutMs, token );

	public static Task<Result<IReadOnlyList<OdooRow>>> SearchReadAsync( string model, Domain? domain = null, IReadOnlyList<string>? fields = null, int? limit = null, int? timeoutMs = null, CancellationToken token = default )
		=> ExecuteAsync<IReadOnlyList<OdooRow>>( model, "search_read",
			limit is null ? [ domain ?? Domain.All, fields ?? [] ] : [ domain ?? Domain.All, fields ?? [], 0, limit ], timeoutMs, token );

	public static Task<Result<int>> CreateAsync( string model, IReadOnlyDictionary<string, object?> values, int? timeoutMs = null, CancellationToken token = default )
		=> ExecuteAsync<int>( model, "create", [ values ], timeoutMs, token );

	public static Task<Result<IReadOnlyList<int>>> CreateManyAsync( string model, IEnumerable<IReadOnlyDictionary<string, object?>> values, int? timeoutMs = null, CancellationToken token = default )
		=> ExecuteAsync<IReadOnlyList<int>>( model, "create", [ values.ToList() ], timeoutMs, token );

	public static Task<Result<bool>> WriteAsync( string model, IReadOnlyList<int> ids, IReadOnlyDictionary<string, object?> values, int? timeoutMs = null, CancellationToken token = default )
		=> ExecuteAsync<bool>( model, "write", [ ids, values ], timeoutMs, token );

	public static Task<Result<bool>> UnlinkAsync( string model, IReadOnlyList<int> ids, int? timeoutMs = null, CancellationToken token = default )
		=> ids.Count == 0
			? Task.FromResult( Result<bool>.Success( true ) )
			: ExecuteAsync<bool>( model, "unlink", [ ids ], timeoutMs, token );

	/// <summary>Opens an hp.pdm.commit for this node; its id.</summary>
	public static Task<Result<int>> CreateCommitAsync( int? timeoutMs = null )
		=> CreateAsync( OdooDefaultsConstants.HP_PDM_COMMIT, new Dictionary<string, object?> { [ "node_by" ] = Settings?.MyNode?.id ?? 0 }, timeoutMs );

	// ------------------------------------------------------------------ result conversion

	/// <summary>
	/// A JSON-RPC result as <typeparamref name="T"/>: bool, int, an id list, rows, a single row
	/// (an object), plain values (object?), the raw JsonElement, or any JSON-deserializable type.
	/// </summary>
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
