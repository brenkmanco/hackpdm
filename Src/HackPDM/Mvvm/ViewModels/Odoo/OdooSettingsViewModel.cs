using System;
using System.Globalization;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Infrastructure.Odoo;
using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.ViewModels.Common;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Mvvm.ViewModels.Odoo;

/// <summary>The Odoo server and login. Nothing is saved unless the area factor is a valid number (the only field checked).</summary>
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
	/// <summary>Whether to connect with HTTPS; turning it off sends the password unencrypted.</summary>
	[ObservableProperty] public partial bool OdooUseHttps { get; set; }

	/// <summary>Raised after a successful save, so a page in its own window can close it.</summary>
	public event Action? Saved;

	/// <summary>Fills the fields from the current Odoo settings.</summary>
	/// <param name="dialogs">Shows validation errors and the saved confirmation.</param>
	/// <param name="session">Optional session status log shared with the Profile Manager.</param>
	public OdooSettingsViewModel( IDialogService dialogs, LoginSession? session = null )
	{
		_dialogs = dialogs;
		_session = session;
		var defaults = OdooDefaults.Instance;
		OdooAddress = defaults.OdooAddress ?? "";
		OdooPort = defaults.OdooPort ?? "";
		OdooDb = defaults.OdooDb ?? "";
		OdooSwKey = defaults.OdooSwKey ?? "";
		OdooAreaFactor = defaults.OdooAreaFactor?.ToString( CultureInfo.CurrentCulture ) ?? "1";
		OdooUser = defaults.OdooUser ?? "";
		OdooPass = defaults.OdooPass ?? "";
		OdooUseHttps = defaults.OdooUseHttps;
	}

	/// <summary>The server's URL from the address and optional port.</summary>
	/// <param name="address">The host name or IP address.</param>
	/// <param name="port">The port; null or empty leaves it out.</param>
	/// <returns>An http URL such as "http://10.0.0.68:8069".</returns>
	public static string UrlOf( string address, string? port, bool useHttps )
		=> OdooEndpoint.BuildUrl( address, port, useHttps );

	/// <summary>Validates the address, port, and area factor, then applies every field to the live Odoo settings and raises <see cref="Saved"/>.</summary>
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
		// validated first: an invalid area factor used to save the other fields and still close the window
		if( !decimal.TryParse( OdooAreaFactor, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal areaFactor ) )
		{
			_session?.LogError( ProfileStatusCategory.Configuration, $"Odoo configuration error: Area Factor '{OdooAreaFactor}' is not a valid decimal number." );
			await _dialogs.ShowMessageAsync( "Area Factor must be a decimal number.", "Invalid Area Factor", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}
		string address = OdooAddress.Trim();
		string trimmedPort = OdooPort?.Trim() ?? string.Empty;
		string url = UrlOf( address, trimmedPort, OdooUseHttps );
		if( !OdooUseHttps && OdooEndpoint.InsecureReason( url, allowPlainHttp: false ) is not null )
		{
			if( !await _dialogs.ConfirmAsync( "Without HTTPS your password is sent unencrypted, and anyone on the network can read it. Save anyway?", "Unencrypted connection", MessageBoxIcon.Warning ) )
			{
				_session?.LogWarning( ProfileStatusCategory.Configuration, $"Odoo configuration for '{url}' was not saved because unencrypted HTTP was declined." );
				return;
			}
			_session?.LogWarning( ProfileStatusCategory.Connection, $"Plain HTTP enabled for {url}; Odoo password will be transmitted unencrypted." );
		}
		var defaults = OdooDefaults.Instance;
		string db = OdooDb.Trim();
		string user = OdooUser.Trim();
		bool endpointOrAccountChanged = !string.Equals( defaults.OdooUrl, url, StringComparison.OrdinalIgnoreCase )
			|| !string.Equals( defaults.OdooDb, db, StringComparison.Ordinal )
			|| !string.Equals( defaults.OdooUser, user, StringComparison.Ordinal )
			|| !string.Equals( defaults.OdooPass, OdooPass, StringComparison.Ordinal );
		if( endpointOrAccountChanged )
		{
			// Credentials or endpoint changed; force a fresh login on next sign-in
			defaults.OdooId = 0;
		}
		defaults.OdooUseHttps = OdooUseHttps;
		defaults.OdooAddress = address;
		defaults.OdooPort = trimmedPort;
		defaults.OdooDb = db;
		defaults.OdooSwKey = OdooSwKey.Trim();
		defaults.OdooAreaFactor = areaFactor;
		defaults.OdooUser = user;
		defaults.OdooPass = OdooPass;
		defaults.OdooUrl = url;

		string userLabel = user.Length > 0 ? user : "(no user)";
		string dbLabel = db.Length > 0 ? db : "(no db)";
		_session?.LogInfo( ProfileStatusCategory.Configuration, $"Saved Odoo configuration: {userLabel} @ {dbLabel} — {url} (Area Factor: {areaFactor})." );
		if( db.Length == 0 || user.Length == 0 )
			_session?.LogWarning( ProfileStatusCategory.Configuration, "Odoo configuration saved with blank database or username; both are required to sign in." );
		if( endpointOrAccountChanged )
			_session?.LogInfo( ProfileStatusCategory.Login, "Odoo endpoint or credentials changed; next login will authenticate a fresh session." );

		// the page is usually shown inside the main window, where nothing else shows that it worked
		await _dialogs.ShowMessageAsync( "Odoo settings saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information );
		Saved?.Invoke();
	}
}
