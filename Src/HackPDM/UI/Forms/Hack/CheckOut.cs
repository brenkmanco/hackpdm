using System.Collections.Generic;
using System.Threading.Tasks;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Infrastructure.Operations;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Forms.Helper;

namespace HackPDM.UI.Forms.Hack;

/// <summary>
/// Presentation layer facade for PDM checkout and uncheckout workflows.
/// Delegates business logic to <see cref="PdmCheckoutOperations"/>.
/// </summary>
internal static class CheckOut
{
	internal static async Task Async_CheckOut( HpEntry[] entries )
	{
		InstanceManager.HFM.ProcessCounter = 0;
		InstanceManager.HFM.SkipCounter = 0;
		InstanceManager.HFM.MaxCount = entries.Length;

		await PdmCheckoutOperations.CheckOutEntriesAsync(
			entries,
			log: (status, msg) => InstanceManager.Session?.Log(status, msg),
			progress: (proc, max) =>
			{
				InstanceManager.HFM.ProcessCounter = proc;
				InstanceManager.Dialog?.SetProgressBar(proc, max);
			});

		InstanceManager.Dialog?.SetProgressBar( InstanceManager.HFM.MaxCount, InstanceManager.HFM.MaxCount );
		await MessageBox.ShowAsync( $"Completed!" );
	}

	internal static async Task Async_UnCheckOut( HpEntry[] entries )
	{
		InstanceManager.HFM.ProcessCounter = 0;
		InstanceManager.HFM.SkipCounter = 0;
		InstanceManager.HFM.MaxCount = entries.Length;

		await PdmCheckoutOperations.UnCheckOutEntriesAsync(
			entries,
			OdooDefaults.Instance.OdooId,
			log: (status, msg) => InstanceManager.Session?.Log(status, msg),
			progress: (proc, max) =>
			{
				InstanceManager.HFM.ProcessCounter = proc;
				InstanceManager.Dialog?.SetProgressBar(proc, max);
			});

		InstanceManager.Dialog?.SetProgressBar( InstanceManager.HFM.MaxCount, InstanceManager.HFM.MaxCount );
		await MessageBox.ShowAsync( $"Completed!" );
	}

	internal static async Task CheckOutEntry( HpEntry? entry )
		=> await PdmCheckoutOperations.CheckOutEntryAsync( entry );

	internal static IEnumerable<HpEntry> FilterCheckoutEntries( HpEntry[] entries )
		=> PdmCheckoutOperations.FilterCheckoutEntries( entries );

	internal static IEnumerable<HpEntry> FilterUnCheckoutEntries( HpEntry[] entries )
		=> PdmCheckoutOperations.FilterUnCheckoutEntries( entries, OdooDefaults.Instance.OdooId );

	internal static async Task UnCheckOutEntry( HpEntry entryModel )
		=> await PdmCheckoutOperations.UnCheckOutEntryAsync( entryModel );
}
