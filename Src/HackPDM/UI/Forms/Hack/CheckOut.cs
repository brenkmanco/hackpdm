using System.Collections.Generic;
using System.Threading.Tasks;

using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;

namespace HackPDM.UI.Forms.Hack;

internal static class CheckOut
{
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