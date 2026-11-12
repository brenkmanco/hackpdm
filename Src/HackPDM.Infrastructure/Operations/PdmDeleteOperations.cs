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

	/// <summary>Permanently deletes (<c>unlink</c>) the hp.version.property records of the given versions.</summary>
	/// <param name="versionIds">The hp.version ids.</param>
	/// <param name="log">Receives progress and error lines; may be null.</param>
	/// <returns>True when the properties were deleted or there were none; false when <paramref name="versionIds"/> is null or empty, or the search or delete failed (for example for a user who is not an administrator).</returns>
	public static Task<bool> PermanentDeleteVersionProperty( IReadOnlyList<int>? versionIds, Action<StatusMessage, string>? log = null )
		=> versionIds is not { Count: > 0 }
			? Task.FromResult( false )
			: UnlinkMatchingAsync( HpVersionProperty.GetHpModel()!, OdooDomain.Where( "version_id", "in", versionIds ), "version properties", log );

	/// <summary>Removes the dependency links of the given versions by clearing their <c>parent_ids</c> and <c>child_ids</c> many2many fields.</summary>
	/// <param name="versionIds">The hp.version ids.</param>
	/// <param name="log">Receives progress and error lines; may be null.</param>
	/// <returns>True when the server accepted the write; false when <paramref name="versionIds"/> is null or empty, or the write failed.</returns>
	/// <remarks>Only the links are removed (command <c>(5, 0, 0)</c>); the versions at the other end are not deleted.</remarks>
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

	/// <summary>Permanently deletes every version of the given entries, with their properties, relationships and stored file contents.</summary>
	/// <param name="entryIds">The hp.entry ids whose versions are deleted.</param>
	/// <param name="log">Receives progress and error lines; may be null.</param>
	/// <returns>True when every step succeeded (or the entries had no versions); false when <paramref name="entryIds"/> is null or empty, or a step failed.</returns>
	/// <remarks>
	/// Runs in order: properties, relationships, the ir.attachment rows holding <c>file_contents</c>, then the versions; it
	/// stops at the first refused step so versions are never left half-deleted. Cannot be undone. The entries themselves are kept.
	/// </remarks>
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
			// one step at a time: once one is refused (a user who is not an administrator) the rest must
			// not run, or the versions lose their links though they are not deleted
			bool properties = await PermanentDeleteVersionProperty( versionIds, log );
			bool relationships = properties && await PermanentDeleteVersionRelationships( versionIds, log );
			// the stored file contents
			attachmentsDeleted = relationships
				&& await UnlinkMatchingAsync( IrAttachment.GetHpModel()!,
					OdooDomain.Where( "res_id", "in", versionIds ).And( "res_model", HpVersion.GetHpModel() ).And( "res_field", "file_contents" ),
					"IR attachments", log );
		}

		bool versionsDeleted = attachmentsDeleted && await UnlinkAsync( HpVersion.GetHpModel()!, versionIds, "versions", log );
		return attachmentsDeleted && versionsDeleted;
	}

	/// <summary>Permanently deletes entries from the server, after all their versions (see <see cref="PermanentDeleteVersions"/>).</summary>
	/// <param name="entryIds">The hp.entry ids.</param>
	/// <param name="log">Receives progress and error lines; may be null.</param>
	/// <returns>True when the versions and then the entries were deleted; false when <paramref name="entryIds"/> is null or empty, or any step failed.</returns>
	/// <remarks>Cannot be undone; normally needs an administrator. Local files are not touched. For a reversible delete use <see cref="SetDeletedAsync"/>.</remarks>
	public static async Task<bool> PermanentDeleteEntry( IReadOnlyList<int>? entryIds, Action<StatusMessage, string>? log = null )
	{
		if( entryIds is not { Count: > 0 } )
			return false;
		bool versions = await PermanentDeleteVersions( entryIds, log );
		return versions && await UnlinkAsync( HpEntry.GetHpModel()!, entryIds, "entries", log );
	}

	/// <summary>Marks entries deleted (or live again) on the server by writing their <c>deleted</c> flag; nothing is removed.</summary>
	/// <param name="entries">The entries; each one's <c>deleted</c> property is updated in memory before the write.</param>
	/// <param name="deleted">True to mark deleted, false to restore.</param>
	/// <param name="log">Receives a line per entry, and an error line for each write the server refused; may be null.</param>
	/// <returns>False when <paramref name="entries"/> is null or empty; otherwise true, even if some writes failed (see the log).</returns>
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
