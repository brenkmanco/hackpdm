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
	private static readonly string[] NoBlobs = [ "preview_image", "file_contents" ];

	private static async Task<HpVersion[]> GetVersionsAsync( int entryId )
		=> await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>( [ entryId ], "version_ids", excludedFields: NoBlobs ) ?? [];

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
				includedFields: [ "name" ],
				insertFields: [ "directory_complete_name" ] ),
			{ Id: not ( null or 0 ) } => await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>(
				recordIds: [ entry.Id.Value ],
				relatedFieldName: $"{nameof( HpEntry.latest_version_id )}.{relation}",
				includedFields: [ "name" ],
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
			.Select( v => new ChildrenRow { Version = v.id ?? 0, Name = v.name, BasePath = Path.Combine( v.WinPathway ?? "" ) } )
			.OrderByDescending( r => r.Version ) ];
	}

	public async Task<List<PropertiesRow>> GetPropertiesAsync( EntryRow entry )
	{
		if( entry.Id is not int id )
			return [];

		HpVersion[] versions = await GetVersionsAsync( id );
		if( versions.Length == 0 )
			return [];

		List<HpVersionProperty[]?> all = await HpVersion.GetAllVersionPropertiesAsync( versions.Ids() );
		var rows = all
			.SelectMany( props => props ?? [] )
			.Where( p => p is { id: not 0 } )
			.Select( p =>
			{
				PropertyType type = p.GetValueType();
				return new PropertiesRow
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
				};
			} );
		return [ .. rows.OrderByDescending( r => r.Version ) ];
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
				OdooCompletePath = OdooValues.Many2OneName( version.HashedValues.GetValueOrDefault( "dir_id" ) ),
			},
		];
	}

	public async Task<byte[]?> GetPreviewImageAsync( int? versionId )
	{
		if( versionId is null or 0 )
			return null;

		HpVersion? version = ( await HpVersion.GetRecordsByIdsAsync( [ versionId.Value ], includedFields: [ "preview_image" ] ) )?.FirstOrDefault();
		if( version is null )
			return null;
		if( version.preview_image is null && !await version.GetPreviewImage() )
			return null;
		return version.preview_image;
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
			[ "preview_image" ] = Convert.ToBase64String( imageBytes ),
		};
		return await OdooRpc.WriteAsync( HpVersion.ModelName, [ versionId ], values );
	}
}
