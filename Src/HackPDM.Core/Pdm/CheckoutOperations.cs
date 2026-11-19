using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;

namespace HackPDM.Core.Pdm;

/// <summary>Checks entries out to this user/node and releases checkouts, toggling local read-only state.</summary>
public static class CheckoutOperations
{
	public static Task CheckOutEntriesAsync(
		HpEntry[] entries,
		Action<StatusMessage, string>? log = null,
		Action<int, int>? progress = null )
	{
		int me = OdooDefaults.Instance.OdooId;
		foreach( HpEntry entry in entries.Where( e => !e.CanCheckOut() ) )
		{
			if( entry.deleted is true )
				log?.Invoke( StatusMessage.ERROR, $"Cannot check out {entry.name}: it is deleted" );
			else if( entry.checkout_user?.id is int holder and not 0 && holder != me )
				log?.Invoke( StatusMessage.ERROR, $"Cannot check out {entry.name}: it is checked out to {entry.checkout_user.name ?? $"user {holder}"}" );
		}
		return ForEachAsync( FilterCheckoutEntries( entries ), CheckOutEntryAsync, "check outs", "Checking out", "check out", log, progress );
	}

	public static Task UnCheckOutEntriesAsync(
		HpEntry[] entries,
		int currentUserId,
		Action<StatusMessage, string>? log = null,
		Action<int, int>? progress = null )
		=> ForEachAsync( FilterUnCheckoutEntries( entries, currentUserId ), UnCheckOutEntryAsync, "uncheck outs", "Unchecking out", "undo the check out of", log, progress );

	private static async Task ForEachAsync(
		List<HpEntry> entries,
		Func<HpEntry, Task<bool>> act,
		string plural,
		string doing,
		string verb,
		Action<StatusMessage, string>? log,
		Action<int, int>? progress )
	{
		log?.Invoke( StatusMessage.INFO, $"{entries.Count} {plural}" );
		int processed = 0;
		foreach( HpEntry entry in entries )
		{
			log?.Invoke( StatusMessage.PROCESSING, $"{doing} {entry.name} ({entry.id})" );
			if( !await act( entry ) )
				log?.Invoke( StatusMessage.ERROR, $"Could not {verb} {entry.name}: {entry.LastError ?? "refused by the server"}" );
			progress?.Invoke( ++processed, entries.Count );
		}
	}

	public static List<HpEntry> FilterCheckoutEntries( IEnumerable<HpEntry> entries )
		=> [ .. entries.Where( entry => entry.CanCheckOut() ) ];

	public static List<HpEntry> FilterUnCheckoutEntries( IEnumerable<HpEntry> entries, int currentUserId )
		=> [ .. entries.Where( entry => entry.CanUnCheckOut( currentUserId ) ) ];

	public static async Task<bool> CheckOutEntryAsync( HpEntry? entry )
	{
		if( entry is null || !entry.CanCheckOut() )
			return false;
		if( entry.latest_version_id?.id is not int versionId || versionId == 0 || OdooDefaults.Instance.MyNode?.id is not int nodeId )
			return false;

		Result<bool> checkedOut = await OdooRpc.ExecuteAsync<bool>( HpVersion.ModelName, "checkout_entry", [ versionId, nodeId ] );
		entry.LastError = checkedOut.Error;
		if( !checkedOut.ValueOr( false ) )
			return false;

		entry.checkout_user = OdooDefaults.Instance.OdooId;
		entry.checkout_date = DateTime.Now;
		entry.checkout_node = nodeId;
		entry.is_checked_out = true;
		SetLocalReadOnly( entry, false );
		return true;
	}

	public static async Task<bool> UnCheckOutEntryAsync( HpEntry? entry )
	{
		if( entry is null || !entry.CanUnCheckOut() )
			return false;

		entry.checkout_user = null;
		entry.checkout_date = null;
		entry.checkout_node = null;
		entry.is_checked_out = false;
		if( !await entry.WriteChangedValuesAsync( "checkout_user", "checkout_date", "checkout_node", "is_checked_out" ) )
			return false;

		SetLocalReadOnly( entry, true );
		return true;
	}

	private static void SetLocalReadOnly( HpEntry entry, bool readOnly )
	{
		string? serverName = entry.HashedValues.GetValueOrDefault( "windows_complete_name" ) as string ?? entry.windows_complete_name;
		if( string.IsNullOrEmpty( serverName ) )
			return;
		FileInfo file = new( PwaPaths.ToAbsolute( serverName )! );
		if( file.Exists )
			file.Attributes = readOnly ? file.Attributes | FileAttributes.ReadOnly : file.Attributes & ~FileAttributes.ReadOnly;
	}
}
