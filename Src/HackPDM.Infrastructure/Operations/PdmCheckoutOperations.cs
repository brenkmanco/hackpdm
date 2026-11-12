using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Core;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Infrastructure.Operations;

/// <summary>
/// Checking entries out to this user and node, and releasing them. A local copy is writable only
/// while it is checked out.
/// </summary>
public static class PdmCheckoutOperations
{
	/// <summary>Checks out every entry that is free to check out, one after another, to this user and node.</summary>
	/// <param name="entries">The entries to check out; deleted ones and ones checked out to someone else are left out with an error line.</param>
	/// <param name="log">Receives a count, a line per entry, and an error line for each refused check-out; may be null.</param>
	/// <param name="progress">Receives (entries done, total) after each entry; may be null.</param>
	/// <remarks>See <see cref="CheckOutEntryAsync"/> for what each check-out does. Failures are logged, not thrown.</remarks>
	public static Task CheckOutEntriesAsync( HpEntry[] entries, Action<StatusMessage, string>? log = null, Action<int, int>? progress = null )
	{
		int me = OdooDefaults.Instance.OdooId;
		// say why a file is left out, instead of a silent "0 check outs ... Completed!"
		foreach( HpEntry entry in entries.Where( e => !e.CanCheckOut() ) )
		{
			if( entry.deleted is true )
				log?.Invoke( StatusMessage.ERROR, $"Cannot check out {entry.name}: it is deleted" );
			else if( entry.checkout_user?.id is int holder and not 0 && holder != me )
				log?.Invoke( StatusMessage.ERROR, $"Cannot check out {entry.name}: it is checked out to {entry.checkout_user.name ?? $"user {holder}"}" );
		}
		return ForEachAsync( FilterCheckoutEntries( entries ), CheckOutEntryAsync, "check outs", "Checking out", "check out", log, progress );
	}

	/// <summary>Undoes the check-out of every entry checked out to <paramref name="currentUserId"/>, one after another, without committing anything.</summary>
	/// <param name="entries">The entries; ones not checked out to <paramref name="currentUserId"/> are skipped silently.</param>
	/// <param name="currentUserId">The user whose check-outs are released.</param>
	/// <param name="log">Receives a count, a line per entry, and an error line for each failure; may be null.</param>
	/// <param name="progress">Receives (entries done, total) after each entry; may be null.</param>
	/// <remarks>Each entry goes through <see cref="UnCheckOutEntryAsync"/>, which checks against the logged-in user, not <paramref name="currentUserId"/>.</remarks>
	public static Task UnCheckOutEntriesAsync( HpEntry[] entries, int currentUserId, Action<StatusMessage, string>? log = null, Action<int, int>? progress = null )
		=> ForEachAsync( FilterUnCheckoutEntries( entries, currentUserId ), UnCheckOutEntryAsync, "uncheck outs", "Unchecking out", "undo the check out of", log, progress );

	private static async Task ForEachAsync( List<HpEntry> entries, Func<HpEntry, Task<bool>> act, string plural, string doing, string verb,
		Action<StatusMessage, string>? log, Action<int, int>? progress )
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

	/// <summary>Selects the entries that can be checked out: not deleted and not checked out to anyone.</summary>
	/// <param name="entries">The entries to filter.</param>
	/// <returns>The matching entries, in order.</returns>
	public static List<HpEntry> FilterCheckoutEntries( IEnumerable<HpEntry> entries )
		=> [ .. entries.Where( entry => entry.CanCheckOut() ) ];

	/// <summary>Selects the entries checked out to <paramref name="currentUserId"/>.</summary>
	/// <param name="entries">The entries to filter.</param>
	/// <param name="currentUserId">The user id to match against <c>checkout_user</c>.</param>
	/// <returns>The matching entries, in order.</returns>
	public static List<HpEntry> FilterUnCheckoutEntries( IEnumerable<HpEntry> entries, int currentUserId )
		=> [ .. entries.Where( entry => entry.CanUnCheckOut( currentUserId ) ) ];

	/// <summary>Checks the entry out to this user and node on the server and makes its local copy writable.</summary>
	/// <param name="entry">The entry; its <c>latest_version_id</c> must be loaded. Its checkout fields and <c>LastError</c> are updated in place.</param>
	/// <returns>
	/// True when checked out; false when <paramref name="entry"/> is null, can't be checked out (deleted or already checked out),
	/// has no latest version, this machine has no node, or the server refused or failed (the error is in <c>entry.LastError</c>).
	/// </returns>
	/// <remarks>Calls <c>hp.version.checkout_entry(version_id, node_id)</c>, then clears the read-only flag of the local file if it exists.</remarks>
	/// <exception cref="IOException">The local file's attributes could not be changed (after the server check-out succeeded).</exception>
	/// <exception cref="UnauthorizedAccessException">The local file's attributes could not be changed (after the server check-out succeeded).</exception>
	public static async Task<bool> CheckOutEntryAsync( HpEntry? entry )
	{
		if( entry is null || !entry.CanCheckOut() )
			return false;
		if( entry.latest_version_id?.id is not int versionId || versionId == 0 || OdooDefaults.Instance.MyNode?.id is not int nodeId )
			return false;

		// hp.version.checkout_entry(version_id, node): the arguments used to be sent as one list,
		// which the server ignored, so nothing was ever checked out
		Result<bool> checkedOut = await OdooRpc.ExecuteAsync<bool>( HpVersion.GetHpModel()!, "checkout_entry", [ versionId, nodeId ] );
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

	/// <summary>Releases the logged-in user's check-out of an entry without committing, and makes its local copy read-only again.</summary>
	/// <param name="entry">The entry; its checkout fields are cleared in place before the server write.</param>
	/// <returns>True when released; false when <paramref name="entry"/> is null, isn't checked out to the logged-in user, or the server write failed.</returns>
	/// <remarks>
	/// Writes empty <c>checkout_user</c>, <c>checkout_date</c>, <c>checkout_node</c> and <c>is_checked_out</c>. Local changes are
	/// not reverted; the file only becomes read-only. If the write fails, the in-memory fields stay cleared.
	/// </remarks>
	/// <exception cref="IOException">The local file's attributes could not be changed (after the server write succeeded).</exception>
	/// <exception cref="UnauthorizedAccessException">The local file's attributes could not be changed (after the server write succeeded).</exception>
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

	/// <summary>Local copies are writable only while checked out.</summary>
	private static void SetLocalReadOnly( HpEntry entry, bool readOnly )
	{
		// stored as "root\dir\file" whatever the client OS
		string? serverName = entry.HashedValues.GetValueOrDefault( "windows_complete_name" ) as string ?? entry.windows_complete_name;
		if( string.IsNullOrEmpty( serverName ) )
			return;
		FileInfo file = new( PwaPaths.ToAbsolute( serverName )! );
		if( file.Exists )
			file.Attributes = readOnly ? file.Attributes | FileAttributes.ReadOnly : file.Attributes & ~FileAttributes.ReadOnly;
	}
}
