using System.Collections;
using System.Globalization;

namespace HackPDM.Core.Odoo.Models;

/// <summary>Minimal reference to an Odoo record by id (used by Many2One / x2many fields).</summary>
public class HpModel
{
	public int? id { get; set; }

	public static implicit operator int?( HpModel? model ) => model?.id;
	public static implicit operator HpModel?( int? id ) => id is null ? null : new() { id = id };
}

/// <summary>Odoo many2one reference: [id, "display_name"].</summary>
public sealed class Many2One : HpModel
{
	public string? name { get; set; }

	public static implicit operator int?( Many2One? model ) => model?.id;
	public static implicit operator Many2One?( int? id ) => id is null ? null : new() { id = id };
	public static implicit operator string?( Many2One? model ) => model?.name;
	public static implicit operator Many2One?( string? name ) => name is null ? null : new() { name = name };
}

/// <summary>Base collection for Odoo one2many and many2many id lists.</summary>
public abstract class MultiRecord : IList<HpModel?>
{
	private readonly List<HpModel?> _items = [];

	protected MultiRecord() { }
	protected MultiRecord( IEnumerable<int> ids )
	{
		if( ids is ICollection<int> col )
			_items.EnsureCapacity( col.Count );
		foreach( int id in ids )
			_items.Add( new HpModel { id = id } );
	}

	public int[] IdValues
	{
		get
		{
			if( _items.Count == 0 )
				return [];
			List<int> ids = new( _items.Count );
			for( int i = 0; i < _items.Count; i++ )
			{
				if( _items[ i ]?.id is int id )
					ids.Add( id );
			}
			return [ .. ids ];
		}
	}
	public static implicit operator int[]( MultiRecord? multi ) => multi?.IdValues ?? [];

	public HpModel? this[ int index ] { get => _items[ index ]; set => _items[ index ] = value; }
	public int Count => _items.Count;
	public bool IsReadOnly => false;
	public void Add( HpModel? item ) => _items.Add( item );
	public void Clear() => _items.Clear();
	public bool Contains( HpModel? item ) => _items.Contains( item );
	public void CopyTo( HpModel?[] array, int arrayIndex ) => _items.CopyTo( array, arrayIndex );
	public IEnumerator<HpModel?> GetEnumerator() => _items.GetEnumerator();
	public int IndexOf( HpModel? item ) => _items.IndexOf( item );
	public void Insert( int index, HpModel? item ) => _items.Insert( index, item );
	public bool Remove( HpModel? item ) => _items.Remove( item );
	public void RemoveAt( int index ) => _items.RemoveAt( index );
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class Many2Many : MultiRecord
{
	public Many2Many() { }
	public Many2Many( IEnumerable<int> ids ) : base( ids ) { }
	public static implicit operator Many2Many( int[] ids ) => new( ids );
}

public sealed class One2Many : MultiRecord
{
	public One2Many() { }
	public One2Many( IEnumerable<int> ids ) : base( ids ) { }
	public static implicit operator One2Many( int[] ids ) => new( ids );
}

/// <summary>Static schema contract implemented by every Odoo model (zero reflection).</summary>
public interface IOdooModel<TSelf> where TSelf : HpRecord<TSelf>, IOdooModel<TSelf>, new()
{
	static abstract string ModelName { get; }
	static abstract string[] AllFields { get; }
}

/// <summary>Common non-generic base for all Odoo model records.</summary>
public abstract class HpRecordBase
{
	public int? id { get; set; }
	public Many2One? commit_id { get; set; }
	public bool IsRecord { get; set; }
	public string[]? ExcludedFields { get; set; }
	public Dictionary<string, object?> HashedValues { get; set; } = [];
	public string? LastError { get; protected internal set; }
	internal int? CreateTimeoutMs { get; set; }

	public abstract string HpModel { get; }
	public abstract bool TrySetField( string field, object? value );
	public abstract Dictionary<string, object?> ToOdoo();
	protected virtual Dictionary<string, object?> ToOdoo( string[]? excludedFields ) => ToOdoo();
	public virtual void CompleteConstruction() { }

	public override string ToString() => $"{id}";
}

/// <summary>Strongly-typed base for every Odoo model: reads, creates, stages, and writes without reflection.</summary>
public abstract class HpRecord<T> : HpRecordBase where T : HpRecord<T>, IOdooModel<T>, new()
{
	public static string[]? UsualExcludedFields { get; set; }

	public override string HpModel => T.ModelName;

	public static string[] GetFields(
		string[]? excluded = null,
		string[]? included = null,
		string[]? insert = null )
	{
		if( excluded is null && included is null && insert is null )
			return T.AllFields;

		HashSet<string>? excludedSet = excluded is { Length: > 4 } ? new HashSet<string>( excluded ) : null;
		HashSet<string>? includedSet = included is { Length: > 4 } ? new HashSet<string>( included ) : null;

		List<string> result = new( T.AllFields.Length + ( insert?.Length ?? 0 ) );
		foreach( string field in T.AllFields )
		{
			if( excluded is not null && ( excludedSet is not null ? excludedSet.Contains( field ) : Array.IndexOf( excluded, field ) >= 0 ) )
				continue;
			if( included is not null && ( includedSet is not null ? !includedSet.Contains( field ) : Array.IndexOf( included, field ) < 0 ) )
				continue;
			result.Add( field );
		}

		if( insert is not null )
		{
			foreach( string extra in insert )
			{
				if( !result.Contains( extra ) )
					result.Add( extra );
			}
		}
		return [ .. result ];
	}

	public Dictionary<string, object?> ComputeValues(
		bool includeEmpty,
		string[]? excludedFields = null,
		string[]? includedFields = null,
		string[]? insertFields = null )
	{
		string[] fields = GetFields( excludedFields, includedFields, insertFields );
		Dictionary<string, object?> outbound = ToOdoo( excludedFields );
		Dictionary<string, object?> values = new( fields.Length + 2 );

		foreach( string field in fields )
		{
			if( outbound.TryGetValue( field, out object? value ) )
			{
				if( includeEmpty || value is not null )
					values[ field ] = value;
			}
			else if( insertFields is not null && Array.IndexOf( insertFields, field ) >= 0 )
			{
				object? inserted = HashedValues.GetValueOrDefault( field );
				if( includeEmpty || inserted is not null )
					values[ field ] = inserted;
			}
		}

		if( id is not null and not 0 )
			values[ nameof( id ) ] = id;
		if( commit_id is not null and not { id: 0 } )
			values[ nameof( commit_id ) ] = commit_id.id;
		return values;
	}

	public async Task<int> CreateCommitAsync()
	{
		Result<int> created = await OdooRpc.CreateCommitAsync();
		LastError = created.Error;
		return ( id = created.Unwrap() ) ?? 0;
	}

	public async Task<int> CreateAsync( bool withEmpty = false, string[]? excludedFields = null )
	{
		Result<int> created = await OdooRpc.CreateAsync(
			HpModel,
			ComputeValues( withEmpty, excludedFields ),
			CreateTimeoutMs ?? OdooRpc.LongTimeout );
		LastError = created.Error;
		id = created.ValueOr( 0 );
		IsRecord = id != 0;
		return id ?? 0;
	}

	public async Task<HpRecordStaged?> StageRecAsync( bool withEmpty = false, string[]? excludedFields = null )
	{
		HpRecordStaged? rec = PreStageRec( withEmpty, excludedFields );
		if( rec is not null )
		{
			await rec.CreateAsync();
			LastError = rec.LastError;
		}
		return rec;
	}

	public async Task<bool> WriteChangedValuesAsync( params string[] fieldNamesToWrite )
	{
		Dictionary<string, object?> outbound = ToOdoo();
		Dictionary<string, object?> values = [];
		foreach( string fieldName in fieldNamesToWrite )
			values[ fieldName ] = outbound.GetValueOrDefault( fieldName );

		Result<bool> written = await OdooRpc.WriteAsync( HpModel, [ id ?? 0 ], values );
		LastError = written.Error;
		return written.ValueOr( false );
	}

	public HpRecordStaged? PreStageRec( bool withEmpty = false, string[]? excludedFields = null )
	{
		if( commit_id is null or { id: 0 } )
			return null;
		if( this is HpRecordStaged stagedSelf )
			return stagedSelf;

		Dictionary<string, object?> values = ComputeValues( withEmpty, excludedFields );
		if( HpModel == HpVersion.ModelName && HashedValues.ContainsKey( "__internal__existing" ) )
			values[ "__internal__existing" ] = true;

		return new HpRecordStaged
		{
			target_model = HpModel,
			commit_id = commit_id,
			committing_id = commit_id,
			payload = values,
			HashedValues = { [ nameof( HpEntry.windows_complete_name ) ] = HashedValues.GetValueOrDefault( nameof( HpEntry.windows_complete_name ) ) },
		};
	}

	public static Task<Result<IReadOnlyList<int>>> MultiCreateAsync(
		IEnumerable<HpRecord<T>> records,
		bool withEmpty = false )
		=> OdooRpc.CreateManyAsync( T.ModelName, records.Select( r => r.ComputeValues( withEmpty ) ) );

	public static Task<Result<IReadOnlyList<int>>> MultiStageAsync(
		IEnumerable<HpRecord<T>> records,
		bool withEmpty = false )
		=> OdooRpc.CreateManyAsync(
			HpRecordStaged.ModelName,
			records.Select( r => r.PreStageRec( withEmpty )?.ComputeValues( false ) ).OfType<Dictionary<string, object?>>() );

	private static T[]? NullIfEmpty( Result<T[]> result )
		=> result.Value is { Length: > 0 } records ? records : null;

	public static async Task<T[]?> GetAllRecordsAsync( string[]? excludedFields = null, string[]? insertFields = null )
		=> NullIfEmpty( await Repository<T>.SearchReadAsync( null, new( excludedFields, null, insertFields ) ) );

	public static async Task<T[]?> GetRecordsBySearchAsync(
		OdooDomain? searchFilter = null,
		string[]? includedFields = null,
		string[]? excludedFields = null,
		string[]? insertFields = null )
		=> NullIfEmpty( await Repository<T>.SearchReadAsync( searchFilter, new( excludedFields, includedFields, insertFields ) ) );

	public static async Task<T?> GetRecordByIdAsync(
		int recordId,
		string[]? excludedFields = null,
		string[]? includedFields = null,
		string[]? insertFields = null )
		=> recordId == 0
			? default
			: ( await GetRecordsByIdsAsync( [ recordId ], excludedFields: excludedFields, includedFields: includedFields, insertFields: insertFields ) )?.FirstOrDefault();

	public static async Task<T[]?> GetRecordsByIdsAsync(
		IReadOnlyList<int>? recordIds,
		OdooDomain? searchFilters = null,
		string[]? excludedFields = null,
		string[]? includedFields = null,
		string[]? insertFields = null,
		int timeoutMs = 90000 )
	{
		if( recordIds is { Count: 0 } )
			return null;
		FieldSelection fields = new( excludedFields, includedFields, insertFields );
		return NullIfEmpty( searchFilters is null
			? await Repository<T>.ReadAsync( recordIds ?? [], fields, timeoutMs )
			: await Repository<T>.SearchReadAsync(
				recordIds is not null ? searchFilters.And( nameof( id ), "in", recordIds ) : searchFilters,
				fields,
				timeoutMs: timeoutMs ) );
	}

	public static async Task<TOther[]?> GetRelatedRecordByIdsAsync<TOther>(
		IReadOnlyList<int> recordIds,
		string relatedFieldName,
		string[]? excludedFields = null,
		string[]? includedFields = null,
		string[]? insertFields = null,
		CancellationToken token = default )
		where TOther : HpRecord<TOther>, IOdooModel<TOther>, new()
	{
		if( recordIds.Count == 0 )
			return null;
		Result<TOther[]> related = await Repository<T>.RelatedAsync<TOther>(
			recordIds,
			relatedFieldName,
			new( excludedFields, includedFields, insertFields ),
			token: token );
		return related.Value is { Length: > 0 } records ? records : null;
	}
}

public static class RecordIds
{
	public static int[] Ids( this IEnumerable<HpRecordBase> models )
		=> [ .. models.Select( m => m.id ).OfType<int>().Where( id => id != 0 ) ];
}

/// <summary>Selects which Odoo fields to request in a read call.</summary>
public readonly record struct FieldSelection( string[]? Excluded = null, string[]? Included = null, string[]? Insert = null )
{
	public static FieldSelection Only( params string[] fields ) => new( Included: fields );

	internal string[] For<T>() where T : HpRecord<T>, IOdooModel<T>, new()
		=> HpRecord<T>.GetFields( Excluded, Included, Insert );
}

/// <summary>Typed fetch and populate layer over OdooRpc for any IOdooModel.</summary>
public static class Repository<T> where T : HpRecord<T>, IOdooModel<T>, new()
{
	public static string Model => T.ModelName;

	public static Task<Result<T[]>> ReadAsync(
		IReadOnlyList<int> ids,
		FieldSelection fields = default,
		int timeoutMs = OdooRpc.LongTimeout,
		CancellationToken token = default )
		=> ids.Count == 0
			? Task.FromResult( Result<T[]>.Success( [] ) )
			: FetchAsync( OdooRpc.ReadAsync( Model, ids, fields.For<T>(), timeoutMs, token ), fields );

	public static Task<Result<T[]>> SearchReadAsync(
		OdooDomain? domain = null,
		FieldSelection fields = default,
		int? limit = null,
		int timeoutMs = OdooRpc.LongTimeout,
		CancellationToken token = default )
		=> FetchAsync( OdooRpc.SearchReadAsync( Model, domain, fields.For<T>(), limit, timeoutMs, token ), fields );

	public static Task<Result<TOther[]>> RelatedAsync<TOther>(
		IReadOnlyList<int> ids,
		string relatedField,
		FieldSelection fields = default,
		int timeoutMs = 60000,
		CancellationToken token = default )
		where TOther : HpRecord<TOther>, IOdooModel<TOther>, new()
		=> Repository<TOther>.FetchAsync(
			OdooRpc.ExecuteAsync<IReadOnlyList<OdooRow>>( Model, "related_browse", [ ids, relatedField, fields.For<TOther>() ], timeoutMs, token ),
			fields );

	internal static async Task<Result<T[]>> FetchAsync( Task<Result<IReadOnlyList<OdooRow>>> fetch, FieldSelection fields )
	{
		Result<IReadOnlyList<OdooRow>> rows = await fetch;
		return rows.Ok ? Result<T[]>.Success( Populate( rows.Value!, fields.Excluded ) ) : Result<T[]>.Fail( rows.Error );
	}

	private static T PopulateOne( IReadOnlyDictionary<string, object?> row, string[]? excludedFields )
	{
		T record = new();
		Assign( record, row );
		record.IsRecord = true;
		record.ExcludedFields = excludedFields;
		record.CompleteConstruction();
		return record;
	}

	public static T[] Populate( IEnumerable<IReadOnlyDictionary<string, object?>> rows, string[]? excludedFields = null )
	{
		if( rows is IReadOnlyCollection<IReadOnlyDictionary<string, object?>> col )
		{
			if( col.Count == 0 )
				return [];
			T[] array = new T[ col.Count ];
			int i = 0;
			foreach( IReadOnlyDictionary<string, object?> row in col )
				array[ i++ ] = PopulateOne( row, excludedFields );
			return array;
		}

		List<T> list = [];
		foreach( IReadOnlyDictionary<string, object?> row in rows )
			list.Add( PopulateOne( row, excludedFields ) );
		return [ .. list ];
	}

	public static void Assign( T record, IReadOnlyDictionary<string, object?> row )
	{
		if( OdooValues.AsInt( row.GetValueOrDefault( nameof( HpRecordBase.id ) ) ) is int id )
			record.id = id;
		foreach( var (field, value) in row )
		{
			if( field == nameof( HpRecordBase.id ) )
				continue;
			if( !record.TrySetField( field, value ) )
				record.HashedValues[ field ] = value;
		}
	}
}

/// <summary>DTOs returned by hp.directory custom RPC methods.</summary>
public sealed record RemoteEntryDto(
	string Name,
	int Id,
	string? Type,
	int? CheckoutUserId,
	string? Category,
	string? FullName,
	long Size,
	int? LatestVersionId,
	int? LatestReleaseId,
	string? LatestChecksum,
	DateTime? LatestDate,
	bool Deleted )
{
	internal static RemoteEntryDto FromRow( string name, IReadOnlyDictionary<string, object?> row ) => new(
		Name: name,
		Id: row.Int( "id" ) ?? 0,
		Type: row.Text( "type" ),
		CheckoutUserId: row.Int( "checkout" ) is int user and not 0 ? user : null,
		Category: row.Text( "category" ),
		FullName: row.Text( "fullname" ),
		Size: row.Long( "size" ),
		LatestVersionId: row.Int( "latest" ),
		LatestReleaseId: row.Int( "release" ),
		LatestChecksum: row.Text( "latest_checksum" ),
		LatestDate: DateTime.TryParse( row.Text( "latest_date" ), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date ) ? date : null,
		Deleted: row.Bool( "deleted" ) );

	internal static IReadOnlyList<RemoteEntryDto> ListFrom( IReadOnlyDictionary<string, object?>? map )
	{
		if( map is null || map.Count == 0 )
			return [];
		List<RemoteEntryDto> list = new( map.Count );
		foreach( var (key, value) in map )
		{
			if( value is IReadOnlyDictionary<string, object?> row )
				list.Add( FromRow( key, row ) );
		}
		return list;
	}
}

public sealed record SubdirectoryDto(
	int Id,
	string Name,
	IReadOnlyList<SubdirectoryDto> Directories,
	IReadOnlyList<RemoteEntryDto> Entries )
{
	internal static SubdirectoryDto FromRow( IReadOnlyDictionary<string, object?> row )
	{
		IReadOnlyDictionary<string, object?>? dirsObj = row.Object( "directories" );
		IReadOnlyList<SubdirectoryDto> dirs;
		if( dirsObj is null || dirsObj.Count == 0 )
		{
			dirs = [];
		}
		else
		{
			List<SubdirectoryDto> list = new( dirsObj.Count );
			foreach( object? value in dirsObj.Values )
			{
				if( value is IReadOnlyDictionary<string, object?> subRow )
					list.Add( FromRow( subRow ) );
			}
			dirs = list;
		}
		return new(
			Id: row.Int( "id" ) ?? 0,
			Name: row.Text( "name" ) ?? "",
			Directories: dirs,
			Entries: RemoteEntryDto.ListFrom( row.Object( "entries" ) ) );
	}
}

public readonly record struct LastAvailableDirectory( int Index, int DirectoryId );
