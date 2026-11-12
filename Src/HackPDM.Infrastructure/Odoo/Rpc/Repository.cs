using HackPDM.Infrastructure.Odoo.Models;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>Which fields of a model to read: all its [OdooProp] fields minus <see cref="Excluded"/>, or only <see cref="Included"/>, plus <see cref="Insert"/> (fields with no property, kept in HashedValues).</summary>
public readonly record struct FieldSelection( string[]? Excluded = null, string[]? Included = null, string[]? Insert = null )
{
	/// <summary>Selects only the listed fields (plus <c>id</c>, which Odoo always returns).</summary>
	/// <param name="fields">The Odoo field names to read.</param>
	/// <returns>A selection with <see cref="Included"/> set.</returns>
	public static FieldSelection Only( params string[] fields ) => new( Included: fields );
	/// <summary>Selects every [OdooProp] field of the model except the listed ones.</summary>
	/// <param name="fields">The Odoo field names to leave out; they are also recorded on each record as excluded.</param>
	/// <returns>A selection with <see cref="Excluded"/> set.</returns>
	public static FieldSelection Without( params string[] fields ) => new( Excluded: fields );

	internal string[] For<T>() where T : HpBaseModelTransport<T>, new() => HpBaseModelTransport<T>.GetFields( Excluded, Included, Insert );
}

/// <summary>
/// Reads records of one model into <typeparamref name="T"/>: one fetch → populate path for every
/// read (read, search_read, smart_read, related_browse) and one <see cref="Assign"/>.
/// </summary>
/// <typeparam name="T">The generated model class (for example <c>HpEntry</c>); its [OdooModel] attribute names the Odoo model.</typeparam>
/// <remarks>
/// Every method returns a <see cref="Result{T}"/>; server and transport errors are reported there, not thrown.
/// The default timeout is <see cref="OdooRpc.LongTimeout"/> because these reads can return many rows.
/// </remarks>
/// <example>
/// <code>
/// // up to 50 non-deleted entries in a directory, reading only two fields
/// Result&lt;HpEntry[]&gt; result = await Repository&lt;HpEntry&gt;.SearchReadAsync(
///     OdooDomain.Where( "dir_id", dirId ).And( "deleted", false ),
///     FieldSelection.Only( "name", "latest_version_id" ),
///     limit: 50 );
/// if( !result.Ok )
///     return Result&lt;int&gt;.Fail( result.Error );
/// foreach( HpEntry entry in result.Value! )
///     Console.WriteLine( $"{entry.id} {entry.name}" );
/// </code>
/// </example>
public static class Repository<T> where T : HpBaseModelTransport<T>, new()
{
	/// <summary>The Odoo model name from <typeparamref name="T"/>'s [OdooModel] attribute.</summary>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> has no [OdooModel] attribute.</exception>
	public static string Model => HpBaseModelTransport<T>.GetHpModel() ?? throw new InvalidOperationException( $"{typeof( T ).Name} has no [OdooModel]" );

	/// <summary>Reads records by id (<c>read</c>) and populates a <typeparamref name="T"/> for each.</summary>
	/// <param name="ids">The record ids. When empty, no call is made.</param>
	/// <param name="fields">Which fields to read; the default reads every [OdooProp] field.</param>
	/// <param name="timeoutMs">Request timeout in milliseconds.</param>
	/// <param name="token">Cancels the request.</param>
	/// <returns>The records in the server's order (empty for no ids), or a failed result with the error.</returns>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> has no [OdooModel] attribute.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
	public static Task<Result<T[]>> ReadAsync( IReadOnlyList<int> ids, FieldSelection fields = default, int timeoutMs = OdooRpc.LongTimeout, CancellationToken token = default )
		=> ids.Count == 0
			? Task.FromResult( Result<T[]>.Success( [] ) )
			: FetchAsync( OdooRpc.ReadAsync( Model, ids, fields.For<T>(), timeoutMs, token ), fields );

	/// <summary>Reads the records matching a domain (<c>search_read</c>) into <typeparamref name="T"/> instances.</summary>
	/// <param name="domain">The filter; every record when null.</param>
	/// <param name="fields">Which fields to read; the default reads every [OdooProp] field.</param>
	/// <param name="limit">Maximum number of records; no limit when null.</param>
	/// <param name="timeoutMs">Request timeout in milliseconds.</param>
	/// <param name="token">Cancels the request.</param>
	/// <returns>The matching records, or a failed result with the error.</returns>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> has no [OdooModel] attribute.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
	public static Task<Result<T[]>> SearchReadAsync( OdooDomain? domain = null, FieldSelection fields = default, int? limit = null, int timeoutMs = OdooRpc.LongTimeout, CancellationToken token = default )
		=> FetchAsync( OdooRpc.SearchReadAsync( Model, domain, fields.For<T>(), limit, timeoutMs, token ), fields );

	/// <summary>Reads the records matching a domain through the HackPDM addon's <c>smart_read</c> model method.</summary>
	/// <param name="domain">The filter; every record when null.</param>
	/// <param name="fields">Which fields to read; the default reads every [OdooProp] field.</param>
	/// <param name="timeoutMs">Request timeout in milliseconds.</param>
	/// <param name="token">Cancels the request.</param>
	/// <returns>The matching records (their ids always included), or a failed result with the error.</returns>
	/// <remarks>Calls <c>smart_read(domain, [], fields)</c>; the empty second argument is an id list.</remarks>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> has no [OdooModel] attribute.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
	public static Task<Result<T[]>> SmartReadAsync( OdooDomain? domain = null, FieldSelection fields = default, int timeoutMs = OdooRpc.LongTimeout, CancellationToken token = default )
		=> FetchAsync( OdooRpc.ExecuteAsync<IReadOnlyList<OdooRow>>( Model, "smart_read", [ domain ?? OdooDomain.All, Array.Empty<int>(), fields.For<T>() ], timeoutMs, token ), fields );

	/// <summary>Follows a relational field of these records and reads the records it points to (the addon's <c>related_browse</c>).</summary>
	/// <typeparam name="TOther">The model class of the related records.</typeparam>
	/// <param name="ids">Ids of the <typeparamref name="T"/> records whose field is followed.</param>
	/// <param name="relatedField">The many2one / x2many field on <typeparamref name="T"/> to follow.</param>
	/// <param name="fields">Which fields of <typeparamref name="TOther"/> to read.</param>
	/// <param name="timeoutMs">Request timeout in milliseconds; 60 seconds by default.</param>
	/// <returns>The related records, or a failed result with the error.</returns>
	/// <remarks>The call is made on <typeparamref name="T"/>'s model and cannot be cancelled.</remarks>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> or <typeparamref name="TOther"/> has no [OdooModel] attribute.</exception>
	public static Task<Result<TOther[]>> RelatedAsync<TOther>( IReadOnlyList<int> ids, string relatedField, FieldSelection fields = default, int timeoutMs = 60000 )
		where TOther : HpBaseModelTransport<TOther>, new()
		=> Repository<TOther>.FetchAsync( OdooRpc.ExecuteAsync<IReadOnlyList<OdooRow>>( Model, "related_browse", [ ids, relatedField, fields.For<TOther>() ], timeoutMs ), fields );

	/// <summary>The one fetch → populate path.</summary>
	internal static async Task<Result<T[]>> FetchAsync( Task<Result<IReadOnlyList<OdooRow>>> fetch, FieldSelection fields )
	{
		Result<IReadOnlyList<OdooRow>> rows = await fetch;
		return rows.Ok ? Result<T[]>.Success( Populate( rows.Value!, fields.Excluded ) ) : Result<T[]>.Fail( rows.Error );
	}

	/// <summary>Builds records from rows: assigns each row, then marks the record as read from the server.</summary>
	/// <param name="rows">Rows as returned by <see cref="OdooRpc"/>.</param>
	/// <param name="excludedFields">Field names left out of the read; stored on each record's ExcludedFields.</param>
	/// <returns>One record per row, with <c>IsRecord</c> set and <c>CompleteConstruction</c> called.</returns>
	public static T[] Populate( IEnumerable<IReadOnlyDictionary<string, object?>> rows, string[]? excludedFields = null )
		=> [ .. rows.Select( row =>
		{
			T record = new();
			Assign( record, row );
			record.IsRecord = true;
			record.ExcludedFields = excludedFields;
			record.CompleteConstruction();
			return record;
		} ) ];

	/// <summary>Copies a row into a record: [OdooProp] properties are set through the generated assignment map, and every other field goes to <c>HashedValues</c>.</summary>
	/// <param name="record">The record to fill; modified in place.</param>
	/// <param name="row">Field name to plain value pairs, as from <see cref="OdooValues.FromJson"/>.</param>
	/// <remarks>The <c>id</c> is set when the row has an integer <c>"id"</c>. Values are converted with <see cref="OdooValueConverter.Convert"/>.</remarks>
	public static void Assign( T record, IReadOnlyDictionary<string, object?> row )
	{
		var map = OdooAssignments<T>.Map;
		if( OdooValues.AsInt( row.GetValueOrDefault( "id" ) ) is int id )
			record.id = id;
		foreach( var (field, value) in row )
		{
			if( map.TryGetValue( field, out var assign ) )
				assign( record, value );
			else
				record.HashedValues[ field ] = value;
		}
	}
}
