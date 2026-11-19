using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;

namespace HackPDM.Core.Pdm;

/// <summary>Logical delete/restore and permanent deletion of entries, versions, links, and attachments.</summary>
public static class DeleteOperations
{
	private const int DeleteTimeoutMs = 100_000;

	private static async Task<bool> UnlinkMatchingAsync( string model, OdooDomain domain, string what, Action<StatusMessage, string>? log )
	{
		Result<IReadOnlyList<int>> found = await OdooRpc.SearchAsync( model, domain, timeoutMs: DeleteTimeoutMs );
		if( !found.Ok )
		{
			log?.Invoke( StatusMessage.ERROR, $"Unable to find {what}: {found.Error}" );
			return false;
		}
		return await UnlinkAsync( model, found.Value!, what, log );
	}

	private static async Task<bool> UnlinkAsync( string model, IReadOnlyList<int> ids, string what, Action<StatusMessage, string>? log )
	{
		if( ids.Count == 0 )
		{
			log?.Invoke( StatusMessage.SKIP, $"No {what} to delete" );
			return true;
		}
		log?.Invoke( StatusMessage.PROCESSING, $"Deleting {what}..." );
		Result<bool> deleted = await OdooRpc.UnlinkAsync( model, ids, DeleteTimeoutMs );
		if( deleted.ValueOr( false ) )
			log?.Invoke( StatusMessage.SUCCESS, $"Deleted {what}: {string.Join( ", ", ids )}" );
		else
			log?.Invoke( StatusMessage.ERROR, $"Unable to delete {what}: {deleted.Error ?? "refused"}" );
		return deleted.ValueOr( false );
	}

	public static Task<bool> PermanentDeleteVersionProperty( IReadOnlyList<int>? versionIds, Action<StatusMessage, string>? log = null )
		=> versionIds is not { Count: > 0 }
			? Task.FromResult( false )
			: UnlinkMatchingAsync( HpVersionProperty.ModelName, OdooDomain.Where( "version_id", "in", versionIds ), "version properties", log );

	public static async Task<bool> PermanentDeleteVersionRelationships( IReadOnlyList<int>? versionIds, Action<StatusMessage, string>? log = null )
	{
		if( versionIds is not { Count: > 0 } )
			return false;
		log?.Invoke( StatusMessage.PROCESSING, "Deleting version relationships..." );
		object?[] clear = [ new object?[] { 5, 0, 0 } ];
		Result<bool> cleared = await OdooRpc.WriteAsync(
			HpVersion.ModelName,
			versionIds,
			new Dictionary<string, object?> { [ "parent_ids" ] = clear, [ "child_ids" ] = clear },
			DeleteTimeoutMs );
		if( cleared.ValueOr( false ) )
			log?.Invoke( StatusMessage.SUCCESS, $"Deleted version relationships of: {string.Join( ", ", versionIds )}" );
		else
			log?.Invoke( StatusMessage.ERROR, $"Unable to delete version relationships: {cleared.Error ?? "refused"}" );
		return cleared.ValueOr( false );
	}

	public static async Task<bool> PermanentDeleteVersions( IReadOnlyList<int>? entryIds, Action<StatusMessage, string>? log = null )
	{
		if( entryIds is not { Count: > 0 } )
			return false;

		HpVersion[]? versions = await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>( entryIds, "version_ids", includedFields: [ "id" ] );
		int[] versionIds = versions?.Ids() ?? [];

		bool attachmentsDeleted = true;
		if( versionIds.Length > 0 )
		{
			bool properties = await PermanentDeleteVersionProperty( versionIds, log );
			bool relationships = properties && await PermanentDeleteVersionRelationships( versionIds, log );
			attachmentsDeleted = relationships && await UnlinkMatchingAsync(
				IrAttachment.ModelName,
				OdooDomain.Where( "res_id", "in", versionIds ).And( "res_model", HpVersion.ModelName ).And( "res_field", "file_contents" ),
				"IR attachments",
				log );
		}

		bool versionsDeleted = attachmentsDeleted && await UnlinkAsync( HpVersion.ModelName, versionIds, "versions", log );
		return attachmentsDeleted && versionsDeleted;
	}

	public static async Task<bool> PermanentDeleteEntry( IReadOnlyList<int>? entryIds, Action<StatusMessage, string>? log = null )
		=> entryIds is { Count: > 0 }
			&& await PermanentDeleteVersions( entryIds, log )
			&& await UnlinkAsync( HpEntry.ModelName, entryIds, "entries", log );

	public static async Task<bool> SetDeletedAsync( HpEntry[]? entries, bool deleted, Action<StatusMessage, string>? log = null )
	{
		if( entries is not { Length: > 0 } )
			return false;
		foreach( HpEntry entry in entries )
		{
			log?.Invoke( StatusMessage.PROCESSING, $"Setting {( deleted ? "InActive" : "Active" )} {entry.name}: {entry.id}" );
			entry.deleted = deleted;
			if( !await entry.WriteChangedValuesAsync( "deleted" ) )
				log?.Invoke( StatusMessage.ERROR, $"Could not mark {entry.name} {( deleted ? "deleted" : "restored" )}: {entry.LastError ?? "refused by the server"}" );
		}
		return true;
	}
}
