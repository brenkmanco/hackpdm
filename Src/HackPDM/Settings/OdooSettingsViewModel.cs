using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;
using HackPDM.Search;

using Microsoft.UI.Xaml.Data;

namespace HackPDM.Settings;

[Bindable]
public sealed partial class OdooSettingsViewModel : ObservableObject
{
	private readonly IDialogService _dialogs;
	private readonly LoginSession? _session;
	private readonly FileManagerState _fileManagerState;
	private readonly SearchOdooViewModel _search;
	private CancellationTokenSource? _loadCts;
	private Task? _credentialsLoadTask;
	private bool _loadingCredentials;
	private bool _userDirty;
	private bool _passDirty;

	[ObservableProperty] public partial string OdooAddress { get; set; }
	[ObservableProperty] public partial string OdooPort { get; set; }
	[ObservableProperty] public partial string OdooDb { get; set; }
	[ObservableProperty] public partial string OdooSwKey { get; set; }
	[ObservableProperty] public partial string OdooAreaFactor { get; set; }
	[ObservableProperty] public partial string OdooUser { get; set; }
	[ObservableProperty] public partial string OdooPass { get; set; }
	[ObservableProperty] public partial bool OdooUseHttps { get; set; }

	partial void OnOdooUserChanged( string value )
	{
		if( !_loadingCredentials )
			_userDirty = true;
	}

	partial void OnOdooPassChanged( string value )
	{
		if( !_loadingCredentials )
			_passDirty = true;
	}

	public OdooSettingsViewModel(
		IDialogService dialogs,
		LoginSession? session,
		FileManagerState fileManagerState,
		SearchOdooViewModel search )
	{
		_dialogs = dialogs;
		_session = session;
		_fileManagerState = fileManagerState;
		_search = search;
		OdooDefaults defaults = OdooDefaults.Instance;
		OdooAddress = defaults.OdooAddress ?? "";
		OdooPort = defaults.OdooPort ?? "";
		OdooDb = defaults.OdooDb ?? "";
		OdooSwKey = defaults.OdooSwKey ?? "";
		OdooAreaFactor = defaults.OdooAreaFactor?.ToString( CultureInfo.CurrentCulture ) ?? "1";
		OdooUser = "";
		OdooPass = "";
		OdooUseHttps = defaults.OdooUseHttps;
	}

	public Task LoadCredentialsAsync()
	{
		CancellationToken token = CtsHelper.Replace( ref _loadCts ).Token;
		return _credentialsLoadTask = LoadCredentialsCoreAsync( token );
	}

	public void CancelPendingLoads() => CtsHelper.CancelAndDispose( ref _loadCts );

	private async Task LoadCredentialsCoreAsync( CancellationToken token )
	{
		try
		{
			OdooDefaults defaults = OdooDefaults.Instance;
			var (user, pass) = await Task.Run( () => ( defaults.OdooUser ?? string.Empty, defaults.OdooPass ?? string.Empty ), token );
			if( token.IsCancellationRequested )
				return;

			_loadingCredentials = true;
			try
			{
				if( !_userDirty && string.IsNullOrEmpty( OdooUser ) )
					OdooUser = user;
				if( !_passDirty && string.IsNullOrEmpty( OdooPass ) )
					OdooPass = pass;
			}
			finally
			{
				_loadingCredentials = false;
			}
		}
		catch( OperationCanceledException ) when( token.IsCancellationRequested )
		{
		}
		catch( Exception ex )
		{
			HackLogger.Warn( "OdooSettings", $"Loading stored Odoo credentials failed: {ex.Message}", ex );
		}
	}

	[RelayCommand]
	private async Task SubmitAsync()
	{
		try
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
			bool useHttps = OdooUseHttps;
			string url = OdooEndpoint.BuildUrl( address, trimmedPort, useHttps );
			if( !useHttps && OdooEndpoint.InsecureReason( url, allowPlainHttp: false ) is not null )
			{
				if( !await _dialogs.ConfirmAsync( "Without HTTPS your password is sent unencrypted. Save anyway?", "Unencrypted connection", MessageBoxIcon.Warning ) )
				{
					_session?.LogWarning( ProfileStatusCategory.Configuration, $"Odoo configuration for '{url}' was not saved because unencrypted HTTP was declined." );
					return;
				}
				_session?.LogWarning( ProfileStatusCategory.Connection, $"Plain HTTP enabled for {url}; Odoo password will be transmitted unencrypted." );
			}

			if( ( !_userDirty || !_passDirty ) && _credentialsLoadTask is { IsCompleted: false } loadTask )
			{
				try { await loadTask; }
				catch { }
			}

			string db = OdooDb.Trim();
			string user = OdooUser.Trim();
			string pass = OdooPass;
			string swKey = OdooSwKey.Trim();

			bool changed = await Task.Run( () =>
			{
				OdooDefaults defaults = OdooDefaults.Instance;
				bool isChanged = !string.Equals( defaults.OdooUrl, url, StringComparison.OrdinalIgnoreCase )
					|| !string.Equals( defaults.OdooDb, db, StringComparison.Ordinal )
					|| !string.Equals( defaults.OdooUser, user, StringComparison.Ordinal )
					|| !string.Equals( defaults.OdooPass, pass, StringComparison.Ordinal );
				if( isChanged )
				{
					defaults.OdooId = 0;
					defaults.Lookups = OdooLookupCache.Empty;
				}

				defaults.OdooUseHttps = useHttps;
				defaults.OdooAddress = address;
				defaults.OdooPort = trimmedPort;
				defaults.OdooDb = db;
				defaults.OdooSwKey = swKey;
				defaults.OdooAreaFactor = areaFactor;
				defaults.OdooUser = user;
				defaults.OdooPass = pass;
				defaults.OdooUrl = url;
				return isChanged;
			} );

			if( changed )
			{
				if( _session is not null )
				{
					_session.IsLoggedIn = false;
					_session.VersionMismatchMessage = null;
				}
				EntryIcons.ClearServerCache();
				_fileManagerState.Reset();
				_search.ResetForSessionChange();
			}

			_session?.LogInfo( ProfileStatusCategory.Configuration, $"Saved Odoo configuration: {( user.Length > 0 ? user : "(no user)" )} @ {( db.Length > 0 ? db : "(no db)" )} — {url}." );
			await _dialogs.ShowMessageAsync( "Odoo settings saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information );
		}
		catch( Exception ex )
		{
			HackLogger.Error( "OdooSettings", $"Saving Odoo settings failed: {ex.Message}", ex );
			_session?.LogError( ProfileStatusCategory.Configuration, $"Failed saving Odoo settings: {ex.Message}" );
			await _dialogs.ShowMessageAsync( $"Unable to save Odoo settings: {ex.Message}", "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error );
		}
	}
}
