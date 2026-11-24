using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;

namespace HackPDM.Core.Pdm;

/// <summary>Logical delete/restore and permanent deletion of entries, versions, links, and attachments.</summary>
public static class DeleteOperations
{
	private const int DeleteTimeoutMs = 100_000;

	private static async Task<bool> UnlinkMatchingAsync( string model, OdooDomain domain, string what, Action<StatusMessage, string>? log, CancellationToken token = default )
	{
		Result<IReadOnlyList<int>> found = await OdooRpc.SearchAsync( model, domain, timeoutMs: DeleteTimeoutMs, token: token ).ConfigureAwait( false );
		if( !found.Ok )
		{
			log?.Invoke( StatusMessage.ERROR, $"Unable to find {what}: {found.Error}" );
			return false;
		}
		return await UnlinkAsync( model, found.Value!, what, log, token ).ConfigureAwait( false );
	}

	private static async Task<bool> UnlinkAsync( string model, IReadOnlyList<int> ids, string what, Action<StatusMessage, string>? log, CancellationToken token = default )
	{
		if( ids.Count == 0 )
		{
			log?.Invoke( StatusMessage.SKIP, $"No {what} to delete" );
			return true;
		}
		log?.Invoke( StatusMessage.PROCESSING, $"Deleting {what}..." );
		Result<bool> deleted = await OdooRpc.UnlinkAsync( model, ids, DeleteTimeoutMs, token ).ConfigureAwait( false );
		if( deleted.ValueOr( false ) )
			log?.Invoke( StatusMessage.SUCCESS, $"Deleted {what}: {string.Join( ", ", ids )}" );
		else
			log?.Invoke( StatusMessage.ERROR, $"Unable to delete {what}: {deleted.Error ?? "refused"}" );
		return deleted.ValueOr( false );
	}

	public static ValueTask<bool> PermanentDeleteVersionProperty( IReadOnlyList<int>? versionIds, Action<StatusMessage, string>? log = null, CancellationToken token = default )
		=> versionIds is not { Count: > 0 }
			? ValueTask.FromResult( false )
			: new ValueTask<bool>( UnlinkMatchingAsync( HpVersionProperty.ModelName, OdooDomain.Where( nameof( HpVersionProperty.version_id ), "in", versionIds ), "version properties", log, token ) );

	public static ValueTask<bool> PermanentDeleteVersionRelationships( IReadOnlyList<int>? versionIds, Action<StatusMessage, string>? log = null, CancellationToken token = default )
	{
		if( versionIds is not { Count: > 0 } )
			return ValueTask.FromResult( false );
		return PermanentDeleteVersionRelationshipsSlowAsync( versionIds, log, token );
	}

	private static async ValueTask<bool> PermanentDeleteVersionRelationshipsSlowAsync( IReadOnlyList<int> versionIds, Action<StatusMessage, string>? log, CancellationToken token )
	{
		log?.Invoke( StatusMessage.PROCESSING, "Deleting version relationships..." );
		object?[] clear = [ new object?[] { 5, 0, 0 } ];
		Result<bool> cleared = await OdooRpc.WriteAsync(
			HpVersion.ModelName,
			versionIds,
			new Dictionary<string, object?> { [ nameof( HpVersion.parent_ids ) ] = clear, [ nameof( HpVersion.child_ids ) ] = clear },
			DeleteTimeoutMs,
			token ).ConfigureAwait( false );
		if( cleared.ValueOr( false ) )
			log?.Invoke( StatusMessage.SUCCESS, $"Deleted version relationships of: {string.Join( ", ", versionIds )}" );
		else
			log?.Invoke( StatusMessage.ERROR, $"Unable to delete version relationships: {cleared.Error ?? "refused"}" );
		return cleared.ValueOr( false );
	}

	public static async Task<bool> PermanentDeleteVersions( IReadOnlyList<int>? entryIds, Action<StatusMessage, string>? log = null, CancellationToken token = default )
	{
		if( entryIds is not { Count: > 0 } )
			return false;

		Result<HpVersion[]> versionsResult = await Repository<HpEntry>.RelatedAsync<HpVersion>(
			entryIds, "version_ids", FieldSelection.Only( nameof( HpVersion.id ) ), DeleteTimeoutMs, token ).ConfigureAwait( false );
		if( !versionsResult.Ok )
		{
			log?.Invoke( StatusMessage.ERROR, $"Unable to read versions for entries: {versionsResult.Error}" );
			return false;
		}
		int[] versionIds = versionsResult.Value?.Ids() ?? [];

		bool attachmentsDeleted = true;
		if( versionIds.Length > 0 )
		{
			bool properties = await PermanentDeleteVersionProperty( versionIds, log, token ).ConfigureAwait( false );
			bool relationships = properties && await PermanentDeleteVersionRelationships( versionIds, log, token ).ConfigureAwait( false );
			attachmentsDeleted = relationships && await UnlinkMatchingAsync(
				IrAttachment.ModelName,
				OdooDomain.Where( nameof( IrAttachment.res_id ), "in", versionIds ).And( nameof( IrAttachment.res_model ), HpVersion.ModelName ).And( "res_field", nameof( HpVersion.file_contents ) ),
				"IR attachments",
				log,
				token ).ConfigureAwait( false );
		}

		bool versionsDeleted = attachmentsDeleted && await UnlinkAsync( HpVersion.ModelName, versionIds, "versions", log, token ).ConfigureAwait( false );
		return attachmentsDeleted && versionsDeleted;
	}

	public static async Task<bool> PermanentDeleteEntry( IReadOnlyList<int>? entryIds, Action<StatusMessage, string>? log = null, CancellationToken token = default )
		=> entryIds is { Count: > 0 }
			&& await PermanentDeleteVersions( entryIds, log, token ).ConfigureAwait( false )
			&& await UnlinkAsync( HpEntry.ModelName, entryIds, "entries", log, token ).ConfigureAwait( false );

	public static async Task<bool> SetDeletedAsync( HpEntry[]? entries, bool deleted, Action<StatusMessage, string>? log = null, CancellationToken token = default )
	{
		if( entries is not { Length: > 0 } )
			return false;

		string stateLabel = deleted ? "InActive" : "Active";
		List<int> ids = new( entries.Length );
		foreach( HpEntry entry in entries )
		{
			if( entry.id is int id and not 0 )
				ids.Add( id );
		}

		if( ids.Count == 0 )
			return true;

		log?.Invoke( StatusMessage.PROCESSING, $"Setting {ids.Count} entry(ies) {stateLabel}..." );
		Dictionary<string, object?> values = new() { [ nameof( HpEntry.deleted ) ] = OdooValues.OutBool( deleted ) };
		Result<bool> written = await OdooRpc.WriteAsync(
			HpEntry.ModelName,
			ids,
			values,
			DeleteTimeoutMs,
			token ).ConfigureAwait( false );

		if( written.ValueOr( false ) )
		{
			foreach( HpEntry entry in entries )
			{
				entry.LastError = null;
				entry.deleted = deleted;
			}
			return true;
		}

		if( ids.Count == 1 )
		{
			string error = written.Error ?? "refused by the server";
			foreach( HpEntry entry in entries )
			{
				entry.LastError = written.Error;
				log?.Invoke( StatusMessage.ERROR, $"Could not mark {entry.name} {( deleted ? "deleted" : "restored" )}: {error}" );
			}
			return false;
		}

		bool allSucceeded = true;
		foreach( HpEntry entry in entries )
		{
			if( entry.id is not int entryId || entryId == 0 )
				continue;
			token.ThrowIfCancellationRequested();
			Result<bool> single = await OdooRpc.WriteAsync( HpEntry.ModelName, [ entryId ], values, DeleteTimeoutMs, token ).ConfigureAwait( false );
			if( single.ValueOr( false ) )
			{
				entry.LastError = null;
				entry.deleted = deleted;
			}
			else
			{
				allSucceeded = false;
				entry.LastError = single.Error;
				string error = single.Error ?? "refused by the server";
				log?.Invoke( StatusMessage.ERROR, $"Could not mark {entry.name} {( deleted ? "deleted" : "restored" )}: {error}" );
			}
		}
		return allSucceeded;
	}
}
