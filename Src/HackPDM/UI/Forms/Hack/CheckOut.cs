using System.Collections.Generic;
using System.Threading.Tasks;

using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Forms.Helper;

namespace HackPDM.UI.Forms.Hack;

internal static class CheckOut
{
	internal static async Task Async_CheckOut( HpEntry[] entries )
	{
		object lockObject = new();
		entries = [ .. CheckOut.FilterCheckoutEntries( entries ) ];
		InstanceManager.HFM.ProcessCounter = 0;
		InstanceManager.HFM.SkipCounter = 0;
		InstanceManager.HFM.MaxCount = entries.Length;
		InstanceManager.Session?.Log( StatusMessage.INFO, $"{InstanceManager.HFM.MaxCount} check outs" );
		for( int i = 0; i < entries.Length; i++ ) {
			HpEntry entryModel = entries[i];

			lock( lockObject ) {
				InstanceManager.Session.Log( StatusMessage.PROCESSING, $"Checking out {entryModel.name} ({entryModel.id})" );
			}
			await CheckOut.CheckOutEntry( entryModel );

			lock( lockObject ) {
				InstanceManager.HFM.ProcessCounter += 1;
				InstanceManager.Dialog?.SetProgressBar( ( InstanceManager.HFM.SkipCounter + InstanceManager.HFM.ProcessCounter ), InstanceManager.HFM.MaxCount );
			}
		}

		InstanceManager.Dialog?.SetProgressBar( InstanceManager.HFM.MaxCount, InstanceManager.HFM.MaxCount );
		await MessageBox.ShowAsync( $"Completed!" );
		//_treeHelper.RestartEntries( OdooDirectoryTree, OdooEntryList );
	}
	internal static async Task Async_UnCheckOut( HpEntry[] entries )
	{
		object lockObject = new();
		InstanceManager.HFM.ProcessCounter = 0;
		InstanceManager.HFM.SkipCounter = 0;
		InstanceManager.HFM.MaxCount = entries.Length;
		InstanceManager.Session.Log( StatusMessage.INFO, $"{InstanceManager.HFM.MaxCount} uncheck outs" );
		for( int i = 0; i < entries.Length; i++ ) {
			HpEntry entryModel = entries[i];

			lock( lockObject ) {
				InstanceManager.Session.Log( StatusMessage.PROCESSING, $"Unchecking out {entryModel.name} ({entryModel.id})" );
			}
			await CheckOut.UnCheckOutEntry( entryModel );

			lock( lockObject ) {
				InstanceManager.HFM.ProcessCounter += 1;
				InstanceManager.Dialog?.SetProgressBar( ( InstanceManager.HFM.SkipCounter + InstanceManager.HFM.ProcessCounter ), InstanceManager.HFM.MaxCount );
			}
		}

		InstanceManager.Dialog?.SetProgressBar( InstanceManager.HFM.MaxCount, InstanceManager.HFM.MaxCount );
		await MessageBox.ShowAsync( $"Completed!" );
		//_treeHelper.RestartEntries( OdooDirectoryTree, OdooEntryList );
	}
	internal static async Task CheckOutEntry( HpEntry? entry )
	{
		if( entry == null )
			return;

		await entry.CheckOut();
	}

	internal static IEnumerable<HpEntry> FilterCheckoutEntries( HpEntry[] entries )
	{
		foreach( HpEntry entry in entries )
		{
			if( entry.checkout_user?.id is null or 0 )
			{
				yield return entry;
			}
		}
	}
	internal static IEnumerable<HpEntry> FilterUnCheckoutEntries( HpEntry[] entries )
	{
		foreach( HpEntry entry in entries )
		{
			if( entry.checkout_user is not null && entry.checkout_user == OdooDefaults.Instance.OdooId )
			{
				yield return entry;
			}
		}
	}
	internal static async Task UnCheckOutEntry( HpEntry entryModel )
	{
		if( entryModel == null )
			return;

		await entryModel.UnCheckOut();
	}
}