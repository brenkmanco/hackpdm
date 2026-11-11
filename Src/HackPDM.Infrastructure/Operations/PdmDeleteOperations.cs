using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Infrastructure.Operations;

/// <summary>
/// Permanent and logical deletes of PDM records: versions with their properties, relationships
/// and stored contents, then the entries.
/// </summary>
public static class PdmDeleteOperations
{
	private const int DeleteTimeoutMs = 100000;

	/// <summary>Deletes the records of <paramref name="model"/> that match <paramref name="domain"/>; true when there were none.</summary>
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
			: UnlinkMatchingAsync( HpVersionProperty.GetHpModel()!, OdooDomain.Where( "version_id", "in", versionIds ), "version properties", log );

	public static async Task<bool> PermanentDeleteVersionRelationships( IReadOnlyList<int>? versionIds, Action<StatusMessage, string>? log = null )
	{
		if( versionIds is not { Count: > 0 } )
			return false;
		// the links are hp.version's parent_ids / child_ids many2many (the hp.version.relationship
		// model is gone from the addon); (5, 0, 0) clears a many2many without deleting the other versions
		log?.Invoke( StatusMessage.PROCESSING, "Deleting version relationships..." );
		object?[] clear = [ new object?[] { 5, 0, 0 } ];
		Result<bool> cleared = await OdooRpc.WriteAsync( HpVersion.GetHpModel()!, versionIds,
			new Dictionary<string, object?> { [ "parent_ids" ] = clear, [ "child_ids" ] = clear }, DeleteTimeoutMs );
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

		// field names are case-sensitive: "ID" selected nothing, i.e. every field incl. binaries
		HpVersion[]? versions = await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>( entryIds, "version_ids", includedFields: [ "id" ] );
		int[] versionIds = versions?.Ids() ?? [];

		bool attachmentsDeleted = true;
		if( versionIds.Length > 0 )
		{
			bool properties = await PermanentDeleteVersionProperty( versionIds, log );
			bool relationships = await PermanentDeleteVersionRelationships( versionIds, log );
			// the stored file contents
			attachmentsDeleted = properties && relationships
				&& await UnlinkMatchingAsync( IrAttachment.GetHpModel()!,
					OdooDomain.Where( "res_id", "in", versionIds ).And( "res_model", HpVersion.GetHpModel() ).And( "res_field", "file_contents" ),
					"IR attachments", log );
		}

		bool versionsDeleted = attachmentsDeleted && await UnlinkAsync( HpVersion.GetHpModel()!, versionIds, "versions", log );
		return attachmentsDeleted && versionsDeleted;
	}

	public static async Task<bool> PermanentDeleteEntry( IReadOnlyList<int>? entryIds, Action<StatusMessage, string>? log = null )
	{
		if( entryIds is not { Count: > 0 } )
			return false;
		bool versions = await PermanentDeleteVersions( entryIds, log );
		return versions && await UnlinkAsync( HpEntry.GetHpModel()!, entryIds, "entries", log );
	}

	/// <summary>Marks the entries deleted (or live again); false when there were none.</summary>
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
