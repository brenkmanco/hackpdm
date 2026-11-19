using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.Pdm;
using HackPDM.Core.SolidWorks;

namespace HackPDM.Core.Services;

/// <summary>Loads detail tabs (history, parents, children, properties, version info) and preview images.</summary>
public interface IEntryDetailsService
{
	Task<List<HistoryRow>> GetHistoryAsync( EntryRow entry );
	Task<List<ParentRow>> GetParentsAsync( EntryRow entry );
	Task<List<ChildrenRow>> GetChildrenAsync( EntryRow entry );
	Task<List<PropertiesRow>> GetPropertiesAsync( EntryRow entry );
	Task<List<VersionRow>> GetVersionInfoAsync( EntryRow entry );
	Task<byte[]?> GetPreviewImageAsync( int? versionId );
	Task<byte[]?> GetLocalPreviewImageAsync( string? filePath );
	Task<Result<bool>> UploadPreviewImageAsync( int versionId, byte[] imageBytes );
}

public sealed class EntryDetailsService : IEntryDetailsService
{
	private static readonly string[] NoBlobs = [ nameof( HpVersion.preview_image ), nameof( HpVersion.file_contents ) ];
	private static readonly string[] HistoryInsertFields = [ "create_uid" ];
	private static readonly string[] PropertyIdInsertFields = [ "version_property_ids" ];

	private static async Task<HpVersion[]> GetVersionsAsync( int entryId )
		=> await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>(
			[ entryId ],
			"version_ids",
			excludedFields: NoBlobs,
			insertFields: HistoryInsertFields ) ?? [];

	public async Task<List<HistoryRow>> GetHistoryAsync( EntryRow entry )
	{
		if( entry.Id is not int id )
			return [];

		var rows = ( await GetVersionsAsync( id ) ).Select( version =>
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

	private static async Task<HpVersion[]> GetRelatedVersionsAsync( EntryRow entry, string relation )
	{
		HpVersion[]? versions = entry switch
		{
			{ LatestId: not ( null or 0 ) } => await HpVersion.GetRelatedRecordByIdsAsync<HpVersion>(
				recordIds: [ entry.LatestId.Value ],
				relatedFieldName: relation,
				includedFields: [ nameof( HpVersion.name ) ],
				insertFields: [ "directory_complete_name" ] ),
			{ Id: not ( null or 0 ) } => await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>(
				recordIds: [ entry.Id.Value ],
				relatedFieldName: $"{nameof( HpEntry.latest_version_id )}.{relation}",
				includedFields: [ nameof( HpVersion.name ) ],
				insertFields: [ "directory_complete_name" ] ),
			_ => null,
		};
		return versions ?? [];
	}

	public async Task<List<ParentRow>> GetParentsAsync( EntryRow entry )
		=> [ .. ( await GetRelatedVersionsAsync( entry, nameof( HpVersion.parent_ids ) ) )
			.Select( v => new ParentRow { Version = v.id ?? 0, Name = v.name, BasePath = v.WinPathway } )
			.OrderByDescending( r => r.Version ) ];

	public async Task<List<ChildrenRow>> GetChildrenAsync( EntryRow entry )
	{
		if( entry.Id is null )
			return [];
		return [ .. ( await GetRelatedVersionsAsync( entry, nameof( HpVersion.child_ids ) ) )
			.Select( static v => new ChildrenRow { Version = v.id ?? 0, Name = v.name, BasePath = v.WinPathway ?? "" } )
			.OrderByDescending( static r => r.Version ) ];
	}

	public async Task<List<PropertiesRow>> GetPropertiesAsync( EntryRow entry )
	{
		if( entry.Id is not int id )
			return [];

		HpVersion[]? versions = await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>(
			[ id ],
			"version_ids",
			includedFields: [ nameof( HpVersion.id ) ],
			insertFields: PropertyIdInsertFields );
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

		HpVersionProperty[]? props = await HpVersionProperty.GetRecordsByIdsAsync( [ .. propIds ] );
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

	public async Task<List<VersionRow>> GetVersionInfoAsync( EntryRow entry )
	{
		if( entry.Id is null )
			return [];

		HpVersion? version = entry.LatestId is null
			? ( await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>( [ entry.Id.Value ], nameof( HpEntry.latest_version_id ), excludedFields: NoBlobs ) )?.FirstOrDefault()
			: await HpVersion.GetRecordByIdAsync( entry.LatestId ?? 0, NoBlobs );
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

	public async Task<byte[]?> GetPreviewImageAsync( int? versionId )
	{
		if( versionId is null or 0 )
			return null;

		HpVersion? version = ( await HpVersion.GetRecordsByIdsAsync( [ versionId.Value ], includedFields: [ nameof( HpVersion.preview_image ) ] ) )?.FirstOrDefault();
		return version?.preview_image is { Length: > 0 } bytes ? bytes : null;
	}

	public Task<byte[]?> GetLocalPreviewImageAsync( string? filePath )
	{
		if( filePath is null || !SwDocumentTypes.IsSolidWorks( filePath ) || SolidWorksUtil.DocMgr is not { } reader )
			return Task.FromResult<byte[]?>( null );
		return Task.Run( () => reader.GetPreviewPng( filePath ) );
	}

	public async Task<Result<bool>> UploadPreviewImageAsync( int versionId, byte[] imageBytes )
	{
		if( versionId <= 0 )
			return Result<bool>.Fail( "Select a committed file version before uploading a preview image." );

		if( PreviewImageValidator.Validate( imageBytes ) is { } validationError )
			return Result<bool>.Fail( validationError );

		Dictionary<string, object?> values = new()
		{
			[ nameof( HpVersion.preview_image ) ] = Convert.ToBase64String( imageBytes ),
		};
		return await OdooRpc.WriteAsync( HpVersion.ModelName, [ versionId ], values );
	}
}
