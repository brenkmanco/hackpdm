using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using HackPDM.Infrastructure.RPC;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>A row returned by read / search_read: field name to value (see <see cref="OdooValues"/>).</summary>
public sealed class OdooRow : Dictionary<string, object?>
{
	/// <summary>Creates an empty row.</summary>
	public OdooRow() { }
	/// <summary>Creates a row holding a copy of <paramref name="values"/>.</summary>
	/// <param name="values">Field name to value pairs to copy into the row.</param>
	/// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
	/// <exception cref="ArgumentException"><paramref name="values"/> contains duplicate keys.</exception>
	public OdooRow( IDictionary<string, object?> values ) : base( values ) { }

	/// <summary>The record id from the row's <c>"id"</c> field.</summary>
	/// <value>The id, or 0 when the row has no integer <c>"id"</c> field.</value>
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
	/// <param name="service">The Odoo JSON-RPC service, such as <c>"common"</c> or <c>"object"</c>.</param>
	/// <param name="method">The service method, such as <c>"login"</c> or <c>"execute"</c>.</param>
	/// <param name="args">Positional arguments; each is converted with <see cref="OdooValues.ToRpc"/> (domains and id lists become arrays).</param>
	/// <param name="timeoutMs">Request timeout in milliseconds; <see cref="ObjectTimeout"/> when null.</param>
	/// <param name="token">Cancels the request.</param>
	/// <returns>
	/// The cloned <c>result</c> element on success (a default <see cref="JsonElement"/> when the server sent none),
	/// or a failed result with the server's error (see <see cref="JsonRpcError.Describe"/>), "no response from the server",
	/// or the transport exception's message chain. Timeouts and HTTP errors are returned as failures, not thrown.
	/// </returns>
	/// <remarks>Posts to <see cref="OdooDefaults.OdooUrl"/> + <see cref="JsonRpcEndpoint"/> through <see cref="OdooTransport.SendJsonAsync"/>.</remarks>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled. This is the only exception rethrown.</exception>
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
			return Result<JsonElement>.Fail( Describe( e ) );
		}
	}

	/// <summary>Calls a model method (<c>object.execute</c>) with the logged-in user and converts the result.</summary>
	/// <typeparam name="T">
	/// The type to convert the result to: <see cref="bool"/>, <see cref="int"/>, <see cref="IReadOnlyList{T}"/> of int (ids),
	/// <see cref="IReadOnlyList{T}"/> of <see cref="OdooRow"/>, a single <see cref="OdooRow"/>, <see cref="object"/> (plain values
	/// from <see cref="OdooValues.FromJson"/>), <see cref="JsonElement"/>, or any type System.Text.Json can deserialize.
	/// </typeparam>
	/// <param name="model">The Odoo model name, such as <c>"hp.entry"</c>.</param>
	/// <param name="method">The model method to call.</param>
	/// <param name="args">Positional arguments after the model and method name.</param>
	/// <param name="timeoutMs">Request timeout in milliseconds; <see cref="ObjectTimeout"/> when null.</param>
	/// <param name="token">Cancels the request.</param>
	/// <returns>The converted value, or a failed result with the call's or the conversion's error.</returns>
	/// <remarks>
	/// Sends the database, <see cref="OdooDefaults.OdooId"/> and password from <see cref="OdooDefaults.Instance"/>, so
	/// <see cref="LoginAsync"/> must have succeeded first. Conversion is lenient: <c>bool</c> is true only for JSON true,
	/// <c>int</c> is 0 for a non-number, and JSON null/false becomes <see langword="default"/> for other types.
	/// </remarks>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
	public static async Task<Result<T>> ExecuteAsync<T>( string model, string method, IReadOnlyList<object?> args, int? timeoutMs = null, CancellationToken token = default )
	{
		Result<JsonElement> raw = await CallAsync( "object", "execute",
			[ Settings.OdooDb, Settings.OdooId, Settings.OdooPass, model, method, .. args ], timeoutMs, token );
		return raw.Ok ? Convert<T>( raw.Value ) : Result<T>.Fail( raw.Error );
	}

	/// <summary>Logs in with the configured database, user name and password (<c>common.login</c>).</summary>
	/// <param name="timeoutMs">Request timeout in milliseconds; <see cref="CommonTimeout"/> when null.</param>
	/// <param name="token">Cancels the request.</param>
	/// <returns>
	/// The Odoo user id, or a failed result when the database, user name or password is not set, the call fails,
	/// or the server rejects the credentials (it returns false, which is reported as "login username or password failed").
	/// </returns>
	/// <remarks>Side effect: on success stores the user id in <see cref="OdooDefaults.OdooId"/>, which every later <see cref="ExecuteAsync{T}"/> call sends.</remarks>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
	public static async Task<Result<int>> LoginAsync( int? timeoutMs = null, CancellationToken token = default )
	{
		if( string.IsNullOrEmpty( Settings?.OdooDb ) || string.IsNullOrEmpty( Settings.OdooUser ) || string.IsNullOrEmpty( Settings.OdooPass ) )
			return Result<int>.Fail( "the database, user name and password must be set" );
		Result<JsonElement> raw = await CallAsync( "common", "login", [ Settings.OdooDb, Settings.OdooUser, Settings.OdooPass ], timeoutMs ?? CommonTimeout, token );
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

	/// <summary>The exception's message followed by its inner exceptions' ("send failed -> connection reset ..."),
	/// since transport errors put the actual cause in the inner exception.</summary>
	internal static string Describe( Exception exc )
	{
		var parts = new List<string>();
		for( Exception? e = exc; e is not null; e = e.InnerException )
			if( parts.Count == 0 || parts[ ^1 ] != e.Message )
				parts.Add( e.Message );
		return string.Join( " -> ", parts );
	}

	// ------------------------------------------------------------------ typed verbs

	/// <summary>Finds the ids of records matching a domain (<c>search</c>).</summary>
	/// <param name="model">The Odoo model name.</param>
	/// <param name="domain">The filter; <see cref="OdooDomain.All"/> when null.</param>
	/// <param name="limit">Maximum number of ids to return (offset 0); no limit when null.</param>
	/// <param name="timeoutMs">Request timeout in milliseconds; <see cref="ObjectTimeout"/> when null.</param>
	/// <param name="token">Cancels the request.</param>
	/// <returns>The matching ids in the model's default order, or a failed result with the error.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
	public static Task<Result<IReadOnlyList<int>>> SearchAsync( string model, OdooDomain? domain = null, int? limit = null, int? timeoutMs = null, CancellationToken token = default )
		=> ExecuteAsync<IReadOnlyList<int>>( model, "search", limit is null ? [ domain ?? OdooDomain.All ] : [ domain ?? OdooDomain.All, 0, limit ], timeoutMs, token );

	/// <summary>Counts the records matching a domain (<c>search_count</c>).</summary>
	/// <param name="model">The Odoo model name.</param>
	/// <param name="domain">The filter; <see cref="OdooDomain.All"/> when null.</param>
	/// <param name="timeoutMs">Request timeout in milliseconds; <see cref="ObjectTimeout"/> when null.</param>
	/// <param name="token">Cancels the request.</param>
	/// <returns>The number of matching records, or a failed result with the error.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
	public static Task<Result<int>> SearchCountAsync( string model, OdooDomain? domain = null, int? timeoutMs = null, CancellationToken token = default )
		=> ExecuteAsync<int>( model, "search_count", [ domain ?? OdooDomain.All ], timeoutMs, token );

	/// <summary>Reads records by id (<c>read</c>).</summary>
	/// <param name="model">The Odoo model name.</param>
	/// <param name="ids">The record ids to read.</param>
	/// <param name="fields">Field names to return; an empty list (the default) makes Odoo return every field.</param>
	/// <param name="timeoutMs">Request timeout in milliseconds; <see cref="ObjectTimeout"/> when null.</param>
	/// <param name="token">Cancels the request.</param>
	/// <returns>One row per record read, or a failed result with the server's or the transport's error.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
	public static Task<Result<IReadOnlyList<OdooRow>>> ReadAsync( string model, IReadOnlyList<int> ids, IReadOnlyList<string>? fields = null, int? timeoutMs = null, CancellationToken token = default )
		=> ExecuteAsync<IReadOnlyList<OdooRow>>( model, "read", [ ids, fields ?? [] ], timeoutMs, token );

	/// <summary>Finds and reads records matching a domain in one call (<c>search_read</c>).</summary>
	/// <param name="model">The Odoo model name.</param>
	/// <param name="domain">The filter; <see cref="OdooDomain.All"/> when null.</param>
	/// <param name="fields">Field names to return; an empty list (the default) makes Odoo return every field.</param>
	/// <param name="limit">Maximum number of rows (offset 0); no limit when null.</param>
	/// <param name="timeoutMs">Request timeout in milliseconds; <see cref="ObjectTimeout"/> when null. Consider <see cref="LongTimeout"/> for large reads.</param>
	/// <param name="token">Cancels the request.</param>
	/// <returns>The matching rows, or a failed result with the error.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
	public static Task<Result<IReadOnlyList<OdooRow>>> SearchReadAsync( string model, OdooDomain? domain = null, IReadOnlyList<string>? fields = null, int? limit = null, int? timeoutMs = null, CancellationToken token = default )
		=> ExecuteAsync<IReadOnlyList<OdooRow>>( model, "search_read",
			limit is null ? [ domain ?? OdooDomain.All, fields ?? [] ] : [ domain ?? OdooDomain.All, fields ?? [], 0, limit ], timeoutMs, token );

	/// <summary>Creates one record (<c>create</c>).</summary>
	/// <param name="model">The Odoo model name.</param>
	/// <param name="values">Field name to value pairs for the new record.</param>
	/// <param name="timeoutMs">Request timeout in milliseconds; <see cref="ObjectTimeout"/> when null.</param>
	/// <param name="token">Cancels the request.</param>
	/// <returns>The new record's id, or a failed result with the error.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
	public static Task<Result<int>> CreateAsync( string model, IReadOnlyDictionary<string, object?> values, int? timeoutMs = null, CancellationToken token = default )
		=> ExecuteAsync<int>( model, "create", [ values ], timeoutMs, token );

	/// <summary>Creates several records in one <c>create</c> call (Odoo's batch create).</summary>
	/// <param name="model">The Odoo model name.</param>
	/// <param name="values">One field-value dictionary per record; enumerated once.</param>
	/// <param name="timeoutMs">Request timeout in milliseconds; <see cref="ObjectTimeout"/> when null.</param>
	/// <param name="token">Cancels the request.</param>
	/// <returns>The new ids in the same order as <paramref name="values"/>, or a failed result with the error.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
	public static Task<Result<IReadOnlyList<int>>> CreateManyAsync( string model, IEnumerable<IReadOnlyDictionary<string, object?>> values, int? timeoutMs = null, CancellationToken token = default )
		=> ExecuteAsync<IReadOnlyList<int>>( model, "create", [ values.ToList() ], timeoutMs, token );

	/// <summary>Sets the same field values on every listed record (<c>write</c>).</summary>
	/// <param name="model">The Odoo model name.</param>
	/// <param name="ids">The records to update.</param>
	/// <param name="values">Field name to new value pairs.</param>
	/// <param name="timeoutMs">Request timeout in milliseconds; <see cref="ObjectTimeout"/> when null.</param>
	/// <param name="token">Cancels the request.</param>
	/// <returns>True when the server reports success, or a failed result with the error.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
	public static Task<Result<bool>> WriteAsync( string model, IReadOnlyList<int> ids, IReadOnlyDictionary<string, object?> values, int? timeoutMs = null, CancellationToken token = default )
		=> ExecuteAsync<bool>( model, "write", [ ids, values ], timeoutMs, token );

	/// <summary>Deletes records on the server (<c>unlink</c>).</summary>
	/// <param name="model">The Odoo model name.</param>
	/// <param name="ids">The records to delete. When empty, no call is made.</param>
	/// <param name="timeoutMs">Request timeout in milliseconds; <see cref="ObjectTimeout"/> when null.</param>
	/// <param name="token">Cancels the request.</param>
	/// <returns>True on success (immediately for an empty id list), or a failed result with the error.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
	public static Task<Result<bool>> UnlinkAsync( string model, IReadOnlyList<int> ids, int? timeoutMs = null, CancellationToken token = default )
		=> ids.Count == 0
			? Task.FromResult( Result<bool>.Success( true ) )
			: ExecuteAsync<bool>( model, "unlink", [ ids ], timeoutMs, token );

	/// <summary>Creates an <c>hp.pdm.commit</c> record owned by this machine's node, to group the versions of one check-in.</summary>
	/// <param name="timeoutMs">Request timeout in milliseconds; <see cref="ObjectTimeout"/> when null.</param>
	/// <returns>The new commit's id, or a failed result with the error.</returns>
	/// <remarks>Sets <c>node_by</c> to <see cref="OdooDefaults.MyNode"/>'s id, or 0 when no node is known. Cannot be cancelled.</remarks>
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
