using System;
using System.Globalization;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Common;
using HackPDM.Core.Odoo;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;

using Microsoft.UI.Xaml.Data;

namespace HackPDM.Settings;

[Bindable]
public partial class OdooSettingsViewModel : ObservableObject
{
	private readonly IDialogService _dialogs;
	private readonly LoginSession? _session;

	[ObservableProperty] public partial string OdooAddress { get; set; }
	[ObservableProperty] public partial string OdooPort { get; set; }
	[ObservableProperty] public partial string OdooDb { get; set; }
	[ObservableProperty] public partial string OdooSwKey { get; set; }
	[ObservableProperty] public partial string OdooAreaFactor { get; set; }
	[ObservableProperty] public partial string OdooUser { get; set; }
	[ObservableProperty] public partial string OdooPass { get; set; }
	[ObservableProperty] public partial bool OdooUseHttps { get; set; }

	public event Action? Saved;

	public OdooSettingsViewModel( IDialogService dialogs, LoginSession? session = null )
	{
		_dialogs = dialogs;
		_session = session;
		OdooDefaults defaults = OdooDefaults.Instance;
		OdooAddress = defaults.OdooAddress ?? "";
		OdooPort = defaults.OdooPort ?? "";
		OdooDb = defaults.OdooDb ?? "";
		OdooSwKey = defaults.OdooSwKey ?? "";
		OdooAreaFactor = defaults.OdooAreaFactor?.ToString( CultureInfo.CurrentCulture ) ?? "1";
		OdooUser = defaults.OdooUser ?? "";
		OdooPass = defaults.OdooPass ?? "";
		OdooUseHttps = defaults.OdooUseHttps;
	}

	[RelayCommand]
	private async Task SubmitAsync()
	{
		if( string.IsNullOrWhiteSpace( OdooAddress ) )
		{
			_session?.LogError( ProfileStatusCategory.Configuration, "Odoo configuration error: Server Address cannot be empty." );
			await _dialogs.ShowMessageAsync( "Odoo Address cannot be empty.", "Invalid Server Address", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}
		if( !string.IsNullOrWhiteSpace( OdooPort ) && ( !int.TryParse( OdooPort.Trim(), out int port ) || port is < 1 or > 65535 ) )
		{
			_session?.LogError( ProfileStatusCategory.Configuration, $"Odoo configuration error: Port '{OdooPort.Trim()}' must be between 1 and 65535." );
			await _dialogs.ShowMessageAsync( "Odoo Port must be a number between 1 and 65535 (or left empty).", "Invalid Port", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}
		if( !decimal.TryParse( OdooAreaFactor, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal areaFactor ) )
		{
			_session?.LogError( ProfileStatusCategory.Configuration, $"Odoo configuration error: Area Factor '{OdooAreaFactor}' is not a valid decimal number." );
			await _dialogs.ShowMessageAsync( "Area Factor must be a decimal number.", "Invalid Area Factor", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}

		string address = OdooAddress.Trim();
		string trimmedPort = OdooPort?.Trim() ?? string.Empty;
		string url = OdooEndpoint.BuildUrl( address, trimmedPort, OdooUseHttps );
		if( !OdooUseHttps && OdooEndpoint.InsecureReason( url, allowPlainHttp: false ) is not null )
		{
			if( !await _dialogs.ConfirmAsync( "Without HTTPS your password is sent unencrypted. Save anyway?", "Unencrypted connection", MessageBoxIcon.Warning ) )
			{
				_session?.LogWarning( ProfileStatusCategory.Configuration, $"Odoo configuration for '{url}' was not saved because unencrypted HTTP was declined." );
				return;
			}
			_session?.LogWarning( ProfileStatusCategory.Connection, $"Plain HTTP enabled for {url}; Odoo password will be transmitted unencrypted." );
		}

		OdooDefaults defaults = OdooDefaults.Instance;
		string db = OdooDb.Trim();
		string user = OdooUser.Trim();
		bool changed = !string.Equals( defaults.OdooUrl, url, StringComparison.OrdinalIgnoreCase )
			|| !string.Equals( defaults.OdooDb, db, StringComparison.Ordinal )
			|| !string.Equals( defaults.OdooUser, user, StringComparison.Ordinal )
			|| !string.Equals( defaults.OdooPass, OdooPass, StringComparison.Ordinal );
		if( changed )
			defaults.OdooId = 0;

		defaults.OdooUseHttps = OdooUseHttps;
		defaults.OdooAddress = address;
		defaults.OdooPort = trimmedPort;
		defaults.OdooDb = db;
		defaults.OdooSwKey = OdooSwKey.Trim();
		defaults.OdooAreaFactor = areaFactor;
		defaults.OdooUser = user;
		defaults.OdooPass = OdooPass;
		defaults.OdooUrl = url;

		_session?.LogInfo( ProfileStatusCategory.Configuration, $"Saved Odoo configuration: {( user.Length > 0 ? user : "(no user)" )} @ {( db.Length > 0 ? db : "(no db)" )} — {url}." );
		await _dialogs.ShowMessageAsync( "Odoo settings saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information );
		Saved?.Invoke();
	}
}
