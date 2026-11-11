using HackPDM.Infrastructure.Odoo.Models;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>Which fields of a model to read: all its [OdooProp] fields minus <see cref="Excluded"/>, or only <see cref="Included"/>, plus <see cref="Insert"/> (fields with no property, kept in HashedValues).</summary>
public readonly record struct FieldSelection( string[]? Excluded = null, string[]? Included = null, string[]? Insert = null )
{
	public static FieldSelection Only( params string[] fields ) => new( Included: fields );
	public static FieldSelection Without( params string[] fields ) => new( Excluded: fields );

	internal string[] For<T>() where T : HpBaseModelTransport<T>, new() => HpBaseModelTransport<T>.GetFields( Excluded, Included, Insert );
}

/// <summary>
/// Reads records of one model into <typeparamref name="T"/>: one fetch → populate path for every
/// read (read, search_read, smart_read, related_browse) and one <see cref="Assign"/>.
/// </summary>
public static class Repository<T> where T : HpBaseModelTransport<T>, new()
{
	public static string Model => HpBaseModelTransport<T>.GetHpModel() ?? throw new InvalidOperationException( $"{typeof( T ).Name} has no [OdooModel]" );

	/// <summary>Records by id (in the server's order). Missing ids are skipped.</summary>
	public static Task<Result<T[]>> ReadAsync( IReadOnlyList<int> ids, FieldSelection fields = default, int timeoutMs = OdooRpc.LongTimeout, CancellationToken token = default )
		=> ids.Count == 0
			? Task.FromResult( Result<T[]>.Success( [] ) )
			: FetchAsync( OdooRpc.ReadAsync( Model, ids, fields.For<T>(), timeoutMs, token ), fields );

	/// <summary>Records matching <paramref name="domain"/> (all records for null).</summary>
	public static Task<Result<T[]>> SearchReadAsync( Domain? domain = null, FieldSelection fields = default, int? limit = null, int timeoutMs = OdooRpc.LongTimeout, CancellationToken token = default )
		=> FetchAsync( OdooRpc.SearchReadAsync( Model, domain, fields.For<T>(), limit, timeoutMs, token ), fields );

	/// <summary>Records matching <paramref name="domain"/> through the addon's smart_read (ids always included).</summary>
	public static Task<Result<T[]>> SmartReadAsync( Domain? domain = null, FieldSelection fields = default, int timeoutMs = OdooRpc.LongTimeout, CancellationToken token = default )
		=> FetchAsync( OdooRpc.ExecuteAsync<IReadOnlyList<OdooRow>>( Model, "smart_read", [ domain ?? Domain.All, Array.Empty<int>(), fields.For<T>() ], timeoutMs, token ), fields );

	/// <summary>The records of <typeparamref name="TOther"/> that <paramref name="relatedField"/> of these records points to (the addon's related_browse).</summary>
	public static Task<Result<TOther[]>> RelatedAsync<TOther>( IReadOnlyList<int> ids, string relatedField, FieldSelection fields = default, int timeoutMs = 60000 )
		where TOther : HpBaseModelTransport<TOther>, new()
		=> Repository<TOther>.FetchAsync( OdooRpc.ExecuteAsync<IReadOnlyList<OdooRow>>( Model, "related_browse", [ ids, relatedField, fields.For<TOther>() ], timeoutMs ), fields );

	/// <summary>The one fetch → populate path.</summary>
	internal static async Task<Result<T[]>> FetchAsync( Task<Result<IReadOnlyList<OdooRow>>> fetch, FieldSelection fields )
	{
		Result<IReadOnlyList<OdooRow>> rows = await fetch;
		return rows.Ok ? Result<T[]>.Success( Populate( rows.Value!, fields.Excluded ) ) : Result<T[]>.Fail( rows.Error! );
	}

	/// <summary>Records from rows: each assigned, then marked as read from the server.</summary>
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

	/// <summary>Sets the record's [OdooProp] properties from a row; other fields go to HashedValues.</summary>
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
