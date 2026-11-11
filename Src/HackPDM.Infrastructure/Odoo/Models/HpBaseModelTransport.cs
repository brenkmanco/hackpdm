using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

using HackPDM.Core.General;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;


namespace HackPDM.Infrastructure.Odoo.Models;

public abstract partial class HpBaseModelTransport : HpBaseModel
{
	// (MVVM) VIEWMODEL
}
public abstract partial class HpBaseModelTransport<T> : HpBaseModelTransport where T : HpBaseModelTransport<T>, new()
{
	/// <summary>Why the last create / write / stage of this record failed; null after a success.</summary>
	public string? LastError { get; protected internal set; }

	/// <summary>The write payload: the selected fields' values (empty ones only with <paramref name="includeEmpty"/>), plus id and commit_id.</summary>
	public Dictionary<string, object?> ComputeValues( bool includeEmpty, string[]? excludedFieldNames = null, string[]? includedFieldNames = null, string[]? insertFieldNames = null )
	{
		var fields = GetFields( excludedFieldNames, includedFieldNames, insertFieldNames );
		var outbound = ToOdoo();
		Dictionary<string, object?> values = [];

		foreach( var field in fields )
		{
			if( outbound.TryGetValue( field, out var value ) )
			{
				if( includeEmpty || value is not null )
					values[ field ] = value;
			}
			else if( insertFieldNames is not null && insertFieldNames.Contains( field ) )
			{
				var inserted = HashedValues.GetValueOrDefault( field );
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

	/// <summary>Opens an hp.pdm.commit; this record's id becomes the commit's.</summary>
	public async virtual Task<int> CreateCommitAsync()
	{
		Result<int> created = await OdooRpc.CreateCommitAsync();
		LastError = created.Error;
		return (id = created.Unwrap()) ?? 0;
	}
	public virtual Task<int> CreateAsync() => CreateAsync( false );
	public virtual Task<HpRecordStaged?> StageRecAsync() => StageRecAsync( false );
	/// <summary>Creates this record on the server; its new id, or 0 (see <see cref="LastError"/>).</summary>
	public async virtual Task<int> CreateAsync( bool withEmpty = false, string[]? excludedFields = null, string[]? insertFIelds = null )
	{
		Result<int> created = await OdooRpc.CreateAsync( HpModel!, ComputeValues( withEmpty, excludedFields ), OdooRpc.LongTimeout );
		LastError = created.Error;
		id = created.ValueOr( 0 );
		IsRecord = id != 0;
		return id ?? 0;
	}
	public async virtual Task<HpRecordStaged?> StageRecAsync( bool withEmpty = false, string[]? excludedFields = null, string[]? insertFIelds = null )
	{
		var rec = PreStageRec( withEmpty, excludedFields, insertFIelds );
		if( rec is not null )
		{
			await rec.CreateAsync();
			LastError = rec.LastError;
		}
		return rec;
	}
	/// <summary>Writes the named properties' current values (as the server expects them).</summary>
	public async virtual Task<bool> WriteChangedValuesAsync( params string[] fieldNamesToWrite )
	{
		var outbound = ToOdoo();
		Dictionary<string, object?> values = [];
		foreach( string fieldName in fieldNamesToWrite )
			values[ fieldName ] = outbound.TryGetValue( fieldName, out var value )
				? value
				: GetType().GetProperty( fieldName, BindingFlags.Public | BindingFlags.Instance )?.GetValue( this );

		Result<bool> written = await OdooRpc.WriteAsync( HpModel!, [ id ?? 0 ], values );
		LastError = written.Error;
		return written.ValueOr( false );
	}
	public virtual HpRecordStaged? PreStageRec( bool withEmpty = false, string[]? excludedFields = null, string[]? insertFIelds = null )
	{
		if( commit_id is null or { id: 0 } ) return null;
		if( HpModel == OdooDefaultsConstants.HP_RECORD_STAGED ) return this as HpRecordStaged;

		Dictionary<string, object?> values = ComputeValues( withEmpty, excludedFields );
		if( HpModel == OdooDefaultsConstants.HP_VERSION && HashedValues.ContainsKey( "__internal__existing" ) )
			values[ "__internal__existing" ] = true;

		HpRecordStaged record = new()
		{
			target_model = HpModel,
			commit_id = commit_id,
			committing_id = (Many2One?)commit_id,
			payload = values,
			HashedValues = { [ "windows_complete_name" ] = HashedValues.GetValueOrDefault( "windows_complete_name" ) },
		};
		return record.PreStageRec( false );
	}
	/// <summary>Creates the records in one call; their ids.</summary>
	public static async Task<IReadOnlyList<int>> MultiCreateAsync( IEnumerable<HpBaseModelTransport<T>> records, bool withEmpty = false )
		=> ( await OdooRpc.CreateManyAsync( GetHpModel()!, records.Select( v => v.ComputeValues( withEmpty ) ) ) ).ValueOr( [] );
	/// <summary>Stages the records (as hp.record.staged) in one call; the staged ids.</summary>
	public static async Task<IReadOnlyList<int>> MultiStageAsync( IEnumerable<HpBaseModelTransport<T>> records, bool withEmpty = false )
		=> ( await OdooRpc.CreateManyAsync( OdooDefaultsConstants.HP_RECORD_STAGED,
			records.Select( v => v.PreStageRec( withEmpty )?.ComputeValues( false ) ).OfType<Dictionary<string, object?>>() ) ).ValueOr( [] );
}
public abstract partial class HpBaseModelTransport<T> 
{
	public override string? HpModel => GetHpModel();
	
	public abstract Dictionary<string, object?> ToOdoo();
	public static string[] GetFields( 
		string[]? excluded = null, 
		string[]? included = null, 
		string[]? insert = null 
		) 
		=> OdooFieldSelectionCache<T>.GetFields( excluded, included, insert ); 
	/// <summary>The Odoo model name (generated from the class's [OdooModel] attribute).</summary>
	public static string? GetHpModel() => HpModelDictionaryGen.GetValueOrDefault(typeof(T).Name);
	
	public override string			ToString() => id.ToString();

	// reads (null when nothing matched or the call failed, as before; Repository<T> gives the error)
	private static T[]? NullIfEmpty( Result<T[]> result ) => result.Value is { Length: > 0 } records ? records : null;

	public static async Task<T[]?> GetAllRecordsAsync( string[]? excludedFields = null, string[]? insertFields = null )
		=> NullIfEmpty( await Repository<T>.SearchReadAsync( null, new( excludedFields, null, insertFields ) ) );
	public static async Task<T[]?> GetRecordsBySmartSearchAsync( OdooDomain? searchFilter = null, string[]? excludedFields = null, string[]? includedFields = null, string[]? insertFields = null )
		=> NullIfEmpty( await Repository<T>.SmartReadAsync( searchFilter, new( excludedFields, includedFields, insertFields ) ) );
	public static async Task<T[]?> GetRecordsBySearchAsync( OdooDomain? searchFilter = null, string[]? includedFields = null, string[]? excludedFields = null, string[]? insertFields = null )
		=> NullIfEmpty( await Repository<T>.SearchReadAsync( searchFilter, new( excludedFields, includedFields, insertFields ) ) );
	public static async Task<T?> GetRecordByIdAsync( int recordId, string[]? excludedFields = null, string[]? includedFields = null, string[]? insertFields = null )
		=> recordId == 0 ? default : ( await GetRecordsByIdsAsync( [ recordId ], excludedFields: excludedFields, includedFields: includedFields, insertFields: insertFields ) )?.FirstOrDefault();
	/// <summary>Records by id; with <paramref name="searchFilters"/>, only those of them (or, without ids, all records) that match.</summary>
	public static async Task<T[]?> GetRecordsByIdsAsync( IReadOnlyList<int>? recordIds, OdooDomain? searchFilters = null, string[]? excludedFields = null, string[]? includedFields = null, string[]? insertFields = null, int timeoutMs = 90000 )
	{
		FieldSelection fields = new( excludedFields, includedFields, insertFields );
		return NullIfEmpty( searchFilters is null
			? await Repository<T>.ReadAsync( recordIds ?? [], fields, timeoutMs )
			: await Repository<T>.SearchReadAsync( recordIds is { Count: > 0 } ? searchFilters.And( "id", "in", recordIds ) : searchFilters, fields, timeoutMs: timeoutMs ) );
	}
	/// <summary>The <typeparamref name="TOther"/> records that <paramref name="relatedFieldName"/> of these records points to.</summary>
	public static async Task<TOther[]?> GetRelatedRecordByIdsAsync<TOther>( IReadOnlyList<int> recordIds, string relatedFieldName, string[]? excludedFields = null, string[]? includedFields = null, string[]? insertFields = null )
		where TOther : HpBaseModelTransport<TOther>, new()
	{
		Result<TOther[]> related = await Repository<T>.RelatedAsync<TOther>( recordIds, relatedFieldName, new( excludedFields, includedFields, insertFields ) );
		return related.Value is { Length: > 0 } records ? records : null;
	}
}
