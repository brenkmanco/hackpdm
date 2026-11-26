using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.Pdm;
using HackPDM.Core.SolidWorks;

namespace HackPDM.Core.Services;

/// <summary>Loads detail tabs (history, parents, children, properties, version info) and preview images.</summary>
public interface IEntryDetailsService
{
	Task<List<HistoryRow>> GetHistoryAsync( EntryRow entry, CancellationToken token = default );
	Task<List<ParentRow>> GetParentsAsync( EntryRow entry, CancellationToken token = default );
	Task<List<ChildrenRow>> GetChildrenAsync( EntryRow entry, CancellationToken token = default );
	Task<List<PropertiesRow>> GetPropertiesAsync( EntryRow entry, CancellationToken token = default );
	Task<List<VersionRow>> GetVersionInfoAsync( EntryRow entry, CancellationToken token = default );
	ValueTask<byte[]?> GetPreviewImageAsync( int? versionId, CancellationToken token = default );
	ValueTask<byte[]?> GetLocalPreviewImageAsync( string? filePath, CancellationToken token = default );
	Task<Result<bool>> UploadPreviewImageAsync( int versionId, byte[] imageBytes, CancellationToken token = default );
}

public sealed class EntryDetailsService : IEntryDetailsService
{
	private static readonly string[] NoBlobs = [ nameof( HpVersion.preview_image ), nameof( HpVersion.file_contents ) ];
	private static readonly string[] HistoryInsertFields = [ "create_uid" ];
	private static readonly string[] PropertyIdInsertFields = [ "version_property_ids" ];

	private static async Task<HpVersion[]> GetVersionsAsync( int entryId, CancellationToken token )
		=> await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>(
			[ entryId ],
			"version_ids",
			excludedFields: NoBlobs,
			insertFields: HistoryInsertFields,
			token: token ).ConfigureAwait( false ) ?? [];

	public async Task<List<HistoryRow>> GetHistoryAsync( EntryRow entry, CancellationToken token = default )
	{
		if( entry.Id is not int id )
			return [];

		var rows = ( await GetVersionsAsync( id, token ).ConfigureAwait( false ) ).Select( version =>
		{
			int? modUser = OdooValues.Many2OneId( version.HashedValues.GetValueOrDefault( "create_uid" ) );
			return new HistoryRow
			{
				Version = version.id ?? 0,
				ModUser = modUser is int u && OdooDefaults.Instance.IdToUser.TryGetValue( u, out HpUser? user ) ? user : null,
				ModDate = version.file_modify_stamp,
				Size = version.file_size,
				RelDate = null,
			};
		} );
		return [ .. rows.OrderByDescending( r => r.Version ) ];
	}

	private static async Task<HpVersion[]> GetRelatedVersionsAsync( EntryRow entry, string relation, CancellationToken token )
	{
		HpVersion[]? versions = entry switch
		{
			{ LatestId: not ( null or 0 ) } => await HpVersion.GetRelatedRecordByIdsAsync<HpVersion>(
				recordIds: [ entry.LatestId.Value ],
				relatedFieldName: relation,
				includedFields: [ nameof( HpVersion.name ) ],
				insertFields: [ "directory_complete_name" ],
				token: token ).ConfigureAwait( false ),
			{ Id: not ( null or 0 ) } => await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>(
				recordIds: [ entry.Id.Value ],
				relatedFieldName: $"{nameof( HpEntry.latest_version_id )}.{relation}",
				includedFields: [ nameof( HpVersion.name ) ],
				insertFields: [ "directory_complete_name" ],
				token: token ).ConfigureAwait( false ),
			_ => null,
		};
		return versions ?? [];
	}

	public async Task<List<ParentRow>> GetParentsAsync( EntryRow entry, CancellationToken token = default )
		=> [ .. ( await GetRelatedVersionsAsync( entry, nameof( HpVersion.parent_ids ), token ).ConfigureAwait( false ) )
			.Select( v => new ParentRow { Version = v.id ?? 0, Name = v.name, BasePath = v.WinPathway } )
			.OrderByDescending( r => r.Version ) ];

	public async Task<List<ChildrenRow>> GetChildrenAsync( EntryRow entry, CancellationToken token = default )
	{
		if( entry.Id is null )
			return [];
		return [ .. ( await GetRelatedVersionsAsync( entry, nameof( HpVersion.child_ids ), token ).ConfigureAwait( false ) )
			.Select( static v => new ChildrenRow { Version = v.id ?? 0, Name = v.name, BasePath = v.WinPathway ?? "" } )
			.OrderByDescending( static r => r.Version ) ];
	}

	public async Task<List<PropertiesRow>> GetPropertiesAsync( EntryRow entry, CancellationToken token = default )
	{
		if( entry.Id is not int id )
			return [];

		HpVersion[]? versions = await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>(
			[ id ],
			"version_ids",
			includedFields: [ nameof( HpVersion.id ) ],
			insertFields: PropertyIdInsertFields,
			token: token ).ConfigureAwait( false );
		if( versions is not { Length: > 0 } )
			return [];

		HashSet<int> propIds = [];
		for( int i = 0; i < versions.Length; i++ )
		{
			IReadOnlyList<int> ids = OdooValues.Ids( versions[ i ].HashedValues.GetValueOrDefault( "version_property_ids" ) );
			for( int j = 0; j < ids.Count; j++ )
				propIds.Add( ids[ j ] );
		}
		if( propIds.Count == 0 )
			return [];

		HpVersionProperty[]? props = await HpVersionProperty.GetRecordsByIdsAsync( [ .. propIds ], token: token ).ConfigureAwait( false );
		if( props is not { Length: > 0 } )
			return [];

		List<PropertiesRow> rows = new( props.Length );
		for( int i = 0; i < props.Length; i++ )
		{
			HpVersionProperty p = props[ i ];
			if( p.id is null or 0 )
				continue;
			PropertyType type = p.GetValueType();
			rows.Add( new PropertiesRow
			{
				Version = p.version_id?.id ?? 0,
				Configuration = p.sw_config_name,
				Name = p.prop_name,
				Property = p.prop_id,
				Type = type,
				ValueData = type switch
				{
					PropertyType.Yesno => p.yesno_value,
					PropertyType.Number => p.number_value,
					PropertyType.Text => p.text_value,
					PropertyType.Date => p.date_value,
					_ => null,
				},
			} );
		}
		rows.Sort( static ( a, b ) => b.Version.CompareTo( a.Version ) );
		return rows;
	}

	public async Task<List<VersionRow>> GetVersionInfoAsync( EntryRow entry, CancellationToken token = default )
	{
		if( entry.Id is null )
			return [];

		HpVersion? version = entry.LatestId is null
			? ( await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>( [ entry.Id.Value ], nameof( HpEntry.latest_version_id ), excludedFields: NoBlobs, token: token ).ConfigureAwait( false ) )?.FirstOrDefault()
			: await HpVersion.GetRecordByIdAsync( entry.LatestId ?? 0, NoBlobs, token: token ).ConfigureAwait( false );
		if( version is null )
			return [];

		return
		[
			new VersionRow
			{
				Id = version.id ?? 0,
				Name = version.name,
				Checksum = version.checksum,
				FileSize = version.file_size,
				DirectoryId = version.dir_id,
				NodeId = version.node_id,
				EntryId = version.entry_id,
				AttachmentId = version.attachment_id,
				ModifyDate = version.file_modify_stamp,
				OdooCompletePath = version.dir_id?.name,
			},
		];
	}

	public ValueTask<byte[]?> GetPreviewImageAsync( int? versionId, CancellationToken token = default )
	{
		if( token.IsCancellationRequested )
			return ValueTask.FromCanceled<byte[]?>( token );
		if( versionId is null or 0 )
			return ValueTask.FromResult<byte[]?>( null );
		return GetPreviewImageSlowAsync( versionId.Value, token );
	}

	private static async ValueTask<byte[]?> GetPreviewImageSlowAsync( int versionId, CancellationToken token )
	{
		HpVersion? version = ( await HpVersion.GetRecordsByIdsAsync( [ versionId ], includedFields: [ nameof( HpVersion.preview_image ) ], token: token ).ConfigureAwait( false ) )?.FirstOrDefault();
		return version?.preview_image is { Length: > 0 } bytes ? bytes : null;
	}

	public ValueTask<byte[]?> GetLocalPreviewImageAsync( string? filePath, CancellationToken token = default )
	{
		if( token.IsCancellationRequested )
			return ValueTask.FromCanceled<byte[]?>( token );
		if( filePath is null || !SwDocumentTypes.IsSolidWorks( filePath ) || SolidWorksUtil.DocMgr is not { } reader )
			return ValueTask.FromResult<byte[]?>( null );
		return new ValueTask<byte[]?>( Task.Run( () =>
		{
			token.ThrowIfCancellationRequested();
			return reader.GetPreviewPng( filePath );
		}, token ) );
	}

	public async Task<Result<bool>> UploadPreviewImageAsync( int versionId, byte[] imageBytes, CancellationToken token = default )
	{
		if( versionId <= 0 )
			return Result<bool>.Fail( "Select a committed file version before uploading a preview image." );
		token.ThrowIfCancellationRequested();

		(string? validationError, string? base64) = await Task.Run( () =>
		{
			token.ThrowIfCancellationRequested();
			if( PreviewImageValidator.Validate( imageBytes ) is { } err )
				return ( err, (string?)null );
			return ( (string?)null, Convert.ToBase64String( imageBytes ) );
		}, token ).ConfigureAwait( false );

		if( validationError is not null )
			return Result<bool>.Fail( validationError );

		Dictionary<string, object?> values = new()
		{
			[ nameof( HpVersion.preview_image ) ] = base64,
		};
		return await OdooRpc.WriteAsync( HpVersion.ModelName, [ versionId ], values, token: token ).ConfigureAwait( false );
	}
}
