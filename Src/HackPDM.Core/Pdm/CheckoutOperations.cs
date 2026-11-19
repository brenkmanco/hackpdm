using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;

namespace HackPDM.Core.Pdm;

/// <summary>Checks entries out to this user/node and releases checkouts, toggling local read-only state.</summary>
public static class CheckoutOperations
{
	private const int UnCheckoutBatchSize = 500;
	private static readonly Dictionary<string, object?> ClearCheckoutValues = new()
	{
		[ nameof( HpEntry.checkout_user ) ] = null,
		[ nameof( HpEntry.checkout_date ) ] = null,
		[ nameof( HpEntry.checkout_node ) ] = null,
		[ nameof( HpEntry.is_checked_out ) ] = false,
	};

	public static async Task CheckOutEntriesAsync(
		HpEntry[] entries,
		Action<StatusMessage, string>? log = null,
		Action<int, int>? progress = null,
		CancellationToken token = default )
	{
		int me = OdooDefaults.Instance.OdooId;
		List<HpEntry> eligible = new( entries.Length );
		for( int i = 0; i < entries.Length; i++ )
		{
			HpEntry entry = entries[ i ];
			if( entry.CanCheckOut() )
			{
				eligible.Add( entry );
				continue;
			}
			if( entry.deleted is true )
				log?.Invoke( StatusMessage.ERROR, $"Cannot check out {entry.name}: it is deleted" );
			else if( entry.checkout_user?.id is int holder and not 0 && holder != me )
				log?.Invoke( StatusMessage.ERROR, $"Cannot check out {entry.name}: it is checked out to {entry.checkout_user.name ?? $"user {holder}"}" );
		}

		log?.Invoke( StatusMessage.INFO, $"{eligible.Count} check outs" );
		int processed = 0;
		foreach( HpEntry entry in eligible )
		{
			token.ThrowIfCancellationRequested();
			log?.Invoke( StatusMessage.PROCESSING, $"Checking out {entry.name} ({entry.id})" );
			if( !await CheckOutEntryAsync( entry, token ) )
				log?.Invoke( StatusMessage.ERROR, $"Could not check out {entry.name}: {entry.LastError ?? "refused by the server"}" );
			progress?.Invoke( ++processed, eligible.Count );
		}
	}

	public static async Task UnCheckOutEntriesAsync(
		HpEntry[] entries,
		int currentUserId,
		Action<StatusMessage, string>? log = null,
		Action<int, int>? progress = null,
		CancellationToken token = default )
	{
		List<HpEntry> eligible = FilterUnCheckoutEntries( entries, currentUserId );
		log?.Invoke( StatusMessage.INFO, $"{eligible.Count} uncheck outs" );
		if( eligible.Count == 0 )
			return;

		int processed = 0;
		for( int offset = 0; offset < eligible.Count; offset += UnCheckoutBatchSize )
		{
			token.ThrowIfCancellationRequested();
			HpEntry[] chunk = OdooRpc.SliceChunk( eligible, offset, UnCheckoutBatchSize );
			List<HpEntry> valid = new( chunk.Length );
			List<int> ids = new( chunk.Length );
			for( int i = 0; i < chunk.Length; i++ )
			{
				HpEntry entry = chunk[ i ];
				log?.Invoke( StatusMessage.PROCESSING, $"Unchecking out {entry.name} ({entry.id})" );
				if( entry.id is int id and not 0 )
				{
					valid.Add( entry );
					ids.Add( id );
				}
			}

			if( ids.Count > 0 )
			{
				Result<bool> written = await OdooRpc.WriteAsync( HpEntry.ModelName, ids, ClearCheckoutValues, token: token );
				bool ok = written.ValueOr( false );
				foreach( HpEntry entry in valid )
				{
					entry.LastError = written.Error;
					if( ok )
					{
						entry.checkout_user = null;
						entry.checkout_date = null;
						entry.checkout_node = null;
						entry.is_checked_out = false;
						SetLocalReadOnly( entry, true );
					}
					else
					{
						log?.Invoke( StatusMessage.ERROR, $"Could not undo the check out of {entry.name}: {written.Error ?? "refused by the server"}" );
					}
				}
			}

			processed += chunk.Length;
			progress?.Invoke( processed, eligible.Count );
		}
	}

	public static List<HpEntry> FilterCheckoutEntries( IEnumerable<HpEntry> entries )
		=> [ .. entries.Where( entry => entry.CanCheckOut() ) ];

	public static List<HpEntry> FilterUnCheckoutEntries( IEnumerable<HpEntry> entries, int currentUserId )
		=> [ .. entries.Where( entry => entry.CanUnCheckOut( currentUserId ) ) ];

	public static async Task<bool> CheckOutEntryAsync( HpEntry? entry, CancellationToken token = default )
	{
		if( entry is null || !entry.CanCheckOut() )
			return false;
		if( entry.latest_version_id?.id is not int versionId || versionId == 0 || OdooDefaults.Instance.MyNode?.id is not int nodeId )
			return false;

		Result<bool> checkedOut = await OdooRpc.ExecuteAsync<bool>( HpVersion.ModelName, "checkout_entry", [ versionId, nodeId ], token: token );
		entry.LastError = checkedOut.Error;
		if( !checkedOut.ValueOr( false ) )
			return false;

		entry.checkout_user = OdooDefaults.Instance.OdooId;
		entry.checkout_date = DateTime.UtcNow;
		entry.checkout_node = nodeId;
		entry.is_checked_out = true;
		SetLocalReadOnly( entry, false );
		return true;
	}

	public static async Task<bool> UnCheckOutEntryAsync( HpEntry? entry, int? currentUserId = null, CancellationToken token = default )
	{
		if( entry is null || !entry.CanUnCheckOut( currentUserId ) || entry.id is not int id || id == 0 )
			return false;

		Result<bool> written = await OdooRpc.WriteAsync( HpEntry.ModelName, [ id ], ClearCheckoutValues, token: token );
		entry.LastError = written.Error;
		if( !written.ValueOr( false ) )
			return false;

		entry.checkout_user = null;
		entry.checkout_date = null;
		entry.checkout_node = null;
		entry.is_checked_out = false;
		SetLocalReadOnly( entry, true );
		return true;
	}

	private static void SetLocalReadOnly( HpEntry entry, bool readOnly )
	{
		string? serverName = entry.windows_complete_name;
		if( string.IsNullOrEmpty( serverName ) || PwaPaths.ToAbsolute( serverName ) is not { Length: > 0 } localPath )
			return;
		if( !readOnly )
		{
			FileOps.ClearReadOnlyIfPresent( localPath );
			return;
		}
		if( !File.Exists( localPath ) )
			return;
		try
		{
			FileAttributes attributes = File.GetAttributes( localPath );
			if( ( attributes & FileAttributes.ReadOnly ) == 0 )
				File.SetAttributes( localPath, attributes | FileAttributes.ReadOnly );
		}
		catch( Exception ex ) when( ex is IOException or UnauthorizedAccessException )
		{
			HackLogger.Warn( nameof( CheckoutOperations ), $"Could not set read-only on '{localPath}': {ex.Message}", ex );
		}
	}
}
