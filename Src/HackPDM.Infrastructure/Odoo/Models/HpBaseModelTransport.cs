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
/// <summary>
/// Base for the client's Odoo model classes: reads records of the model through
/// <see cref="HackPDM.Infrastructure.Odoo.Repository{T}"/> and creates, writes and stages them through
/// <see cref="HackPDM.Infrastructure.Odoo.OdooRpc"/>.
/// </summary>
/// <typeparam name="T">
/// The concrete model class itself (curiously recurring pattern); its <c>[OdooModel]</c> attribute names the
/// Odoo model, and its <c>[OdooProp]</c> properties are the fields read and written.
/// </typeparam>
public abstract partial class HpBaseModelTransport<T> : HpBaseModelTransport where T : HpBaseModelTransport<T>, new()
{
	/// <summary>Why the last create / write / stage of this record failed; null after a success.</summary>
	public string? LastError { get; protected internal set; }

	/// <summary>Builds the field/value dictionary sent to the server when this record is created or staged.</summary>
	/// <param name="includeEmpty">Whether fields whose value is <see langword="null"/> are included (as null).</param>
	/// <param name="excludedFieldNames">Model fields to leave out.</param>
	/// <param name="includedFieldNames">When not <see langword="null"/>, only these model fields are considered.</param>
	/// <param name="insertFieldNames">Extra field names to add; a name that is not an <c>[OdooProp]</c> field takes its value from <c>HashedValues</c>.</param>
	/// <returns>
	/// The selected fields' values in their outbound (RPC) form from <see cref="ToOdoo"/>, plus <c>id</c> and
	/// <c>commit_id</c> (as an int) when they are set and not 0, regardless of the field filters.
	/// </returns>
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

	/// <summary>Creates a new hp.pdm.commit on the server for the current node.</summary>
	/// <returns>The new commit's id.</returns>
	/// <remarks>
	/// Overwrites this record's <c>id</c> with the commit's id and sets <see cref="LastError"/>; it is meant to
	/// be called on an <see cref="HpPDMCommit"/>. The commit's <c>node_by</c> is the configured node.
	/// </remarks>
	/// <exception cref="HackPDM.Infrastructure.Odoo.OdooException">The server did not create the commit.</exception>
	public async virtual Task<int> CreateCommitAsync()
	{
		Result<int> created = await OdooRpc.CreateCommitAsync();
		LastError = created.Error;
		return (id = created.Unwrap()) ?? 0;
	}
	/// <summary>Creates this record on the server, sending only its non-empty fields.</summary>
	/// <returns>The new record id, or 0 when the create failed (see <see cref="LastError"/>).</returns>
	/// <remarks>Same as <see cref="CreateAsync(bool, string[], string[])"/> with <c>withEmpty</c> false.</remarks>
	public virtual Task<int> CreateAsync() => CreateAsync( false );
	/// <summary>Stages this record (as an hp.record.staged row of its commit), sending only its non-empty fields.</summary>
	/// <returns>The staged record, or <see langword="null"/> when this record has no <c>commit_id</c>.</returns>
	/// <remarks>Same as <see cref="StageRecAsync(bool, string[], string[])"/> with <c>withEmpty</c> false.</remarks>
	public virtual Task<HpRecordStaged?> StageRecAsync() => StageRecAsync( false );
	/// <summary>Creates this record on the server with a <c>create</c> call.</summary>
	/// <param name="withEmpty">Whether to send fields whose value is <see langword="null"/>.</param>
	/// <param name="excludedFields">Model fields not to send.</param>
	/// <param name="insertFIelds">Currently ignored: it is not passed on to <see cref="ComputeValues"/>.</param>
	/// <returns>The new record id, or 0 when the create failed (see <see cref="LastError"/>).</returns>
	/// <remarks>
	/// On return <c>id</c> holds the new id (0 on failure), <see cref="HackPDM.Domain.OdooModels.Models.HpBaseModel.IsRecord"/>
	/// says whether it was created, and <see cref="LastError"/> holds the server's error or <see langword="null"/>.
	/// Uses the long RPC timeout.
	/// </remarks>
	public async virtual Task<int> CreateAsync( bool withEmpty = false, string[]? excludedFields = null, string[]? insertFIelds = null )
	{
		Result<int> created = await OdooRpc.CreateAsync( HpModel!, ComputeValues( withEmpty, excludedFields ), OdooRpc.LongTimeout );
		LastError = created.Error;
		id = created.ValueOr( 0 );
		IsRecord = id != 0;
		return id ?? 0;
	}
	/// <summary>
	/// Stages this record in its commit: wraps its values in an <see cref="HpRecordStaged"/> (see
	/// <see cref="PreStageRec"/>) and creates that on the server.
	/// </summary>
	/// <param name="withEmpty">Whether to send fields whose value is <see langword="null"/>.</param>
	/// <param name="excludedFields">Model fields not to send.</param>
	/// <param name="insertFIelds">Passed to <see cref="PreStageRec"/>, which currently ignores it.</param>
	/// <returns>
	/// The staged record (its <c>id</c> is 0 when the create failed), or <see langword="null"/> when this record
	/// has no <c>commit_id</c>.
	/// </returns>
	/// <remarks>Copies the staged record's <see cref="LastError"/> to this record.</remarks>
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
	/// <summary>Writes the current values of the named fields of this record to the server.</summary>
	/// <param name="fieldNamesToWrite">The Odoo field names to write.</param>
	/// <returns><see langword="true"/> when the server accepted the write; otherwise <see langword="false"/> and <see cref="LastError"/> says why.</returns>
	/// <remarks>
	/// Values come from <see cref="ToOdoo"/> (the outbound RPC form); a name that is not an <c>[OdooProp]</c>
	/// field is read by reflection from the public property of that name, and becomes <see langword="null"/> when
	/// there is none. Uses the <c>write</c> method on this record's id (0 when it has none).
	/// </remarks>
	/// <example>
	/// <code>
	/// entry.checkout_user = null;
	/// bool ok = await entry.WriteChangedValuesAsync( "checkout_user", "checkout_node" );
	/// </code>
	/// </example>
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
	/// <summary>
	/// Builds, without sending it, the <see cref="HpRecordStaged"/> that stages this record in its commit.
	/// </summary>
	/// <param name="withEmpty">Whether the payload includes fields whose value is <see langword="null"/>.</param>
	/// <param name="excludedFields">Model fields to leave out of the payload.</param>
	/// <param name="insertFIelds">Currently ignored.</param>
	/// <returns>
	/// <see langword="null"/> when this record has no <c>commit_id</c>; this record itself when it already is an
	/// hp.record.staged; otherwise a new staged record whose <c>target_model</c> is this model and whose
	/// <c>payload</c> is <see cref="ComputeValues"/>.
	/// </returns>
	/// <remarks>
	/// The staged record carries this record's <c>windows_complete_name</c> from <c>HashedValues</c>. For
	/// hp.version records with an <c>__internal__existing</c> marker the payload also gets that flag.
	/// </remarks>
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
	/// <summary>Creates several records of this model with a single <c>create</c> call.</summary>
	/// <param name="records">The records to create; their own <c>id</c>s are not updated.</param>
	/// <param name="withEmpty">Whether to send fields whose value is <see langword="null"/>.</param>
	/// <returns>The new ids in the order of <paramref name="records"/>, or an empty list when the call failed (the error is discarded).</returns>
	public static async Task<IReadOnlyList<int>> MultiCreateAsync( IEnumerable<HpBaseModelTransport<T>> records, bool withEmpty = false )
		=> ( await OdooRpc.CreateManyAsync( GetHpModel()!, records.Select( v => v.ComputeValues( withEmpty ) ) ) ).ValueOr( [] );
	/// <summary>Stages several records (as hp.record.staged rows) with a single <c>create</c> call.</summary>
	/// <param name="records">The records to stage; each needs a <c>commit_id</c>, and records without one are silently skipped.</param>
	/// <param name="withEmpty">Whether the payloads include fields whose value is <see langword="null"/>.</param>
	/// <returns>The ids of the hp.record.staged rows, or an empty list when the call failed (the error is discarded).</returns>
	/// <remarks>Each payload is built with <see cref="PreStageRec"/>.</remarks>
	public static async Task<IReadOnlyList<int>> MultiStageAsync( IEnumerable<HpBaseModelTransport<T>> records, bool withEmpty = false )
		=> ( await OdooRpc.CreateManyAsync( OdooDefaultsConstants.HP_RECORD_STAGED,
			records.Select( v => v.PreStageRec( withEmpty )?.ComputeValues( false ) ).OfType<Dictionary<string, object?>>() ) ).ValueOr( [] );
}
public abstract partial class HpBaseModelTransport<T> 
{
	public override string? HpModel => GetHpModel();
	
	/// <summary>Converts this record's <c>[OdooProp]</c> properties to the values sent over JSON-RPC.</summary>
	/// <returns>Field name to outbound value (for example a Many2One as its id); generated per model class.</returns>
	public abstract Dictionary<string, object?> ToOdoo();
	/// <summary>Selects which of this model's <c>[OdooProp]</c> field names to read or send.</summary>
	/// <param name="excluded">Fields to leave out.</param>
	/// <param name="included">When not <see langword="null"/>, only these model fields are kept.</param>
	/// <param name="insert">Extra field names appended (even ones that are not model fields), without duplicates.</param>
	/// <returns>The field names, in the model's declaration order followed by the inserted ones.</returns>
	/// <remarks>Uses the generated field list and <see cref="OdooFieldSelector.Select"/>.</remarks>
	public static string[] GetFields( 
		string[]? excluded = null, 
		string[]? included = null, 
		string[]? insert = null 
		) 
		=> OdooFieldSelectionCache<T>.GetFields( excluded, included, insert ); 
	/// <summary>Gets the Odoo model name of <typeparamref name="T"/>, such as <c>hp.entry</c>.</summary>
	/// <returns>The name from the class's <c>[OdooModel]</c> attribute (via the generated map), or <see langword="null"/> when the class has none.</returns>
	public static string? GetHpModel() => HpModelDictionaryGen.GetValueOrDefault(typeof(T).Name);
	
	public override string			ToString() => $"{id}";

	// reads (null when nothing matched or the call failed, as before; Repository<T> gives the error)
	private static T[]? NullIfEmpty( Result<T[]> result ) => result.Value is { Length: > 0 } records ? records : null;

	/// <summary>Reads every record of this model with <c>search_read</c>.</summary>
	/// <param name="excludedFields">Fields not to read.</param>
	/// <param name="insertFields">Extra fields to read; ones that are not <c>[OdooProp]</c> fields land in <c>HashedValues</c>.</param>
	/// <returns>The records, or <see langword="null"/> when there are none or the call failed (the error is discarded).</returns>
	public static async Task<T[]?> GetAllRecordsAsync( string[]? excludedFields = null, string[]? insertFields = null )
		=> NullIfEmpty( await Repository<T>.SearchReadAsync( null, new( excludedFields, null, insertFields ) ) );
	/// <summary>Reads the records matching a domain through the addon's <c>smart_read</c> method.</summary>
	/// <param name="searchFilter">The Odoo domain; <see langword="null"/> matches all records.</param>
	/// <param name="excludedFields">Fields not to read.</param>
	/// <param name="includedFields">When not <see langword="null"/>, only these model fields are read.</param>
	/// <param name="insertFields">Extra fields to read; ones that are not <c>[OdooProp]</c> fields land in <c>HashedValues</c>.</param>
	/// <returns>The records, or <see langword="null"/> when none matched or the call failed (the error is discarded).</returns>
	/// <seealso cref="HackPDM.Infrastructure.Odoo.Repository{T}.SmartReadAsync"/>
	public static async Task<T[]?> GetRecordsBySmartSearchAsync( OdooDomain? searchFilter = null, string[]? excludedFields = null, string[]? includedFields = null, string[]? insertFields = null )
		=> NullIfEmpty( await Repository<T>.SmartReadAsync( searchFilter, new( excludedFields, includedFields, insertFields ) ) );
	/// <summary>Reads the records matching a domain with <c>search_read</c>.</summary>
	/// <param name="searchFilter">The Odoo domain; <see langword="null"/> matches all records.</param>
	/// <param name="includedFields">When not <see langword="null"/>, only these model fields are read. Note this comes before <paramref name="excludedFields"/>, unlike the other readers.</param>
	/// <param name="excludedFields">Fields not to read.</param>
	/// <param name="insertFields">Extra fields to read; ones that are not <c>[OdooProp]</c> fields land in <c>HashedValues</c>.</param>
	/// <returns>The records, or <see langword="null"/> when none matched or the call failed (the error is discarded).</returns>
	public static async Task<T[]?> GetRecordsBySearchAsync( OdooDomain? searchFilter = null, string[]? includedFields = null, string[]? excludedFields = null, string[]? insertFields = null )
		=> NullIfEmpty( await Repository<T>.SearchReadAsync( searchFilter, new( excludedFields, includedFields, insertFields ) ) );
	/// <summary>Reads one record by id.</summary>
	/// <param name="recordId">The record id; 0 returns <see langword="null"/> without calling the server.</param>
	/// <param name="excludedFields">Fields not to read.</param>
	/// <param name="includedFields">When not <see langword="null"/>, only these model fields are read.</param>
	/// <param name="insertFields">Extra fields to read; ones that are not <c>[OdooProp]</c> fields land in <c>HashedValues</c>.</param>
	/// <returns>The record, or <see langword="null"/> when it doesn't exist or the call failed.</returns>
	public static async Task<T?> GetRecordByIdAsync( int recordId, string[]? excludedFields = null, string[]? includedFields = null, string[]? insertFields = null )
		=> recordId == 0 ? default : ( await GetRecordsByIdsAsync( [ recordId ], excludedFields: excludedFields, includedFields: includedFields, insertFields: insertFields ) )?.FirstOrDefault();
	/// <summary>Reads records by id, optionally narrowed by a domain.</summary>
	/// <param name="recordIds">
	/// The ids to read. Without <paramref name="searchFilters"/>, <see langword="null"/> or empty reads nothing;
	/// with filters, <see langword="null"/> or empty searches all records.
	/// </param>
	/// <param name="searchFilters">When set, a <c>search_read</c> with this domain (and <c>id in recordIds</c>) is used instead of <c>read</c>.</param>
	/// <param name="excludedFields">Fields not to read.</param>
	/// <param name="includedFields">When not <see langword="null"/>, only these model fields are read.</param>
	/// <param name="insertFields">Extra fields to read; ones that are not <c>[OdooProp]</c> fields land in <c>HashedValues</c>.</param>
	/// <param name="timeoutMs">The RPC timeout in milliseconds.</param>
	/// <returns>
	/// The records in the server's order (missing ids are skipped), or <see langword="null"/> when nothing was
	/// found or the call failed (the error is discarded).
	/// </returns>
	public static async Task<T[]?> GetRecordsByIdsAsync( IReadOnlyList<int>? recordIds, OdooDomain? searchFilters = null, string[]? excludedFields = null, string[]? includedFields = null, string[]? insertFields = null, int timeoutMs = 90000 )
	{
		FieldSelection fields = new( excludedFields, includedFields, insertFields );
		return NullIfEmpty( searchFilters is null
			? await Repository<T>.ReadAsync( recordIds ?? [], fields, timeoutMs )
			: await Repository<T>.SearchReadAsync( recordIds is { Count: > 0 } ? searchFilters.And( "id", "in", recordIds ) : searchFilters, fields, timeoutMs: timeoutMs ) );
	}
	/// <summary>Reads the records that a relational field of the given records points to.</summary>
	/// <typeparam name="TOther">The model class of the related records (for example <see cref="HpVersion"/> for an entry's <c>latest_version_id</c>).</typeparam>
	/// <param name="recordIds">The ids of the <typeparamref name="T"/> records whose field to follow.</param>
	/// <param name="relatedFieldName">The many2one / one2many / many2many field of <typeparamref name="T"/> to follow.</param>
	/// <param name="excludedFields">Fields of <typeparamref name="TOther"/> not to read.</param>
	/// <param name="includedFields">When not <see langword="null"/>, only these <typeparamref name="TOther"/> fields are read.</param>
	/// <param name="insertFields">Extra fields to read; ones that are not <c>[OdooProp]</c> fields land in <c>HashedValues</c>.</param>
	/// <returns>The related records, or <see langword="null"/> when there are none or the call failed (the error is discarded).</returns>
	/// <remarks>Calls the addon's <c>related_browse</c> method with a 60 second timeout.</remarks>
	public static async Task<TOther[]?> GetRelatedRecordByIdsAsync<TOther>( IReadOnlyList<int> recordIds, string relatedFieldName, string[]? excludedFields = null, string[]? includedFields = null, string[]? insertFields = null )
		where TOther : HpBaseModelTransport<TOther>, new()
	{
		Result<TOther[]> related = await Repository<T>.RelatedAsync<TOther>( recordIds, relatedFieldName, new( excludedFields, includedFields, insertFields ) );
		return related.Value is { Length: > 0 } records ? records : null;
	}
}
