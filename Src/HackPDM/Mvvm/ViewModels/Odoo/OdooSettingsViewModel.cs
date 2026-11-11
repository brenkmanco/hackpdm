using System;
using System.Globalization;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Infrastructure.Odoo;
using HackPDM.Mvvm.ViewModels.Common;

namespace HackPDM.Mvvm.ViewModels.Odoo;

/// <summary>The Odoo server and login. Nothing is saved unless every field is valid.</summary>
public partial class OdooSettingsViewModel : ObservableObject
{
	private readonly IDialogService _dialogs;

	[ObservableProperty] public partial string OdooAddress { get; set; }
	[ObservableProperty] public partial string OdooPort { get; set; }
	[ObservableProperty] public partial string OdooDb { get; set; }
	[ObservableProperty] public partial string OdooSwKey { get; set; }
	[ObservableProperty] public partial string OdooAreaFactor { get; set; }
	[ObservableProperty] public partial string OdooUser { get; set; }
	[ObservableProperty] public partial string OdooPass { get; set; }

	/// <summary>Raised after a successful save, so a page in its own window can close it.</summary>
	public event Action? Saved;

	public OdooSettingsViewModel( IDialogService dialogs )
	{
		_dialogs = dialogs;
		var defaults = OdooDefaults.Instance;
		OdooAddress = defaults.OdooAddress ?? "";
		OdooPort = defaults.OdooPort ?? "";
		OdooDb = defaults.OdooDb ?? "";
		OdooSwKey = defaults.OdooSwKey ?? "";
		OdooAreaFactor = defaults.OdooAreaFactor?.ToString( CultureInfo.CurrentCulture ) ?? "1";
		OdooUser = defaults.OdooUser ?? "";
		OdooPass = defaults.OdooPass ?? "";
	}

	/// <summary>The server's URL from the address and optional port.</summary>
	public static string UrlOf( string address, string? port )
		=> string.IsNullOrEmpty( port ) ? $"http://{address}" : $"http://{address}:{port}";

	[RelayCommand]
	private async Task SubmitAsync()
	{
		// validated first: an invalid area factor used to save the other fields and still close the window
		if( !decimal.TryParse( OdooAreaFactor, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal areaFactor ) )
		{
			await _dialogs.ShowMessageAsync( "Area Factor must be a decimal number" );
			return;
		}
		var defaults = OdooDefaults.Instance;
		defaults.OdooAddress = OdooAddress;
		defaults.OdooPort = OdooPort;
		defaults.OdooDb = OdooDb;
		defaults.OdooSwKey = OdooSwKey;
		defaults.OdooAreaFactor = areaFactor;
		defaults.OdooUser = OdooUser;
		defaults.OdooPass = OdooPass;
		defaults.OdooUrl = UrlOf( OdooAddress, OdooPort );
		// the page is usually shown inside the main window, where nothing else shows that it worked
		await _dialogs.ShowMessageAsync( "Odoo settings saved" );
		Saved?.Invoke();
	}
}
