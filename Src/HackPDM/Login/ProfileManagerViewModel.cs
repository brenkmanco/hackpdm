using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;
using HackPDM.Shell;

using Microsoft.UI.Xaml.Data;

namespace HackPDM.Login;

[Bindable]
public partial class ProfileManagerViewModel : ObservableObject, IDisposable
{
	private const string ProfileExtension = ".json";

	private readonly LoginSession _session;
	private readonly IDialogService _dialogs;
	private readonly IFilePickerService _pickers;
	private readonly INavigationService _navigation;
	private readonly Action? _accountChanged;
	private readonly Func<IReadOnlyList<(StatusMessage Status, string Category, string Message)>>? _inspectSettings;
	private bool _attached;

	public ProfileManagerViewModel(
		LoginSession session,
		IDialogService dialogs,
		IFilePickerService pickers,
		INavigationService navigation,
		Action? accountChanged = null,
		Func<IReadOnlyList<(StatusMessage Status, string Category, string Message)>>? inspectSettings = null )
	{
		_session = session;
		_dialogs = dialogs;
		_pickers = pickers;
		_navigation = navigation;
		_accountChanged = accountChanged;
		_inspectSettings = inspectSettings;
		Attach();
	}

	public ObservableCollection<BasicStatusMessage> OStatus { get; } = [];
	public ObservableCollection<BasicStatusMessage> FilteredStatus { get; } = [];

	public string[] CategoryFilterOptions { get; } =
	[
		"All Areas",
		ProfileStatusCategory.Login,
		ProfileStatusCategory.Connection,
		ProfileStatusCategory.Configuration,
		ProfileStatusCategory.Settings,
	];

	public string[] LevelFilterOptions { get; } =
	[
		"All Levels",
		"Errors & Warnings",
		"Errors Only",
		"Info & Success",
	];

	public bool IsLoggedIn
	{
		get => _session.IsLoggedIn;
		set
		{
			if( _session.IsLoggedIn == value )
				return;
			_session.IsLoggedIn = value;
			OnPropertyChanged();
			OnPropertyChanged( nameof( ConnectionBadgeText ) );
		}
	}

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( IsBusy ) )]
	[NotifyPropertyChangedFor( nameof( IsIdle ) )]
	[NotifyPropertyChangedFor( nameof( LoginButtonText ) )]
	public partial bool IsLoggingIn { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( IsBusy ) )]
	[NotifyPropertyChangedFor( nameof( IsIdle ) )]
	[NotifyPropertyChangedFor( nameof( TestConnectionButtonText ) )]
	public partial bool IsTestingConnection { get; set; }

	[ObservableProperty] public partial string LoginStatusText { get; set; } = string.Empty;
	[ObservableProperty] public partial string ConnectionTargetText { get; set; } = FormatConnectionTarget();
	[ObservableProperty] public partial int SelectedCategoryFilterIndex { get; set; }
	[ObservableProperty] public partial int SelectedLevelFilterIndex { get; set; }
	[ObservableProperty] public partial bool HasBanner { get; set; }
	[ObservableProperty] public partial StatusMessage BannerStatus { get; set; } = StatusMessage.INFO;
	[ObservableProperty] public partial string BannerTitle { get; set; } = string.Empty;
	[ObservableProperty] public partial string BannerMessage { get; set; } = string.Empty;
	[ObservableProperty] public partial bool ShowOpenConfigurationsAction { get; set; }
	[ObservableProperty] public partial string StatusCountsText { get; set; } = "No messages";
	[ObservableProperty] public partial string EmptyHintTitle { get; set; } = "Ready to connect";
	[ObservableProperty] public partial string EmptyHintSubtitle { get; set; } = "Click Odoo Login below to authenticate and sync workspace metadata.";

	public bool IsBusy => IsLoggingIn || IsTestingConnection;
	public bool IsIdle => !IsBusy;
	public string LoginButtonText => IsLoggingIn ? "Signing in…" : "Odoo Login";
	public string TestConnectionButtonText => IsTestingConnection ? "Testing…" : "Test Connection";
	public string ConnectionBadgeText => IsLoggedIn ? "Signed In" : "Not Signed In";

	partial void OnSelectedCategoryFilterIndexChanged( int value ) => ApplyFilters();
	partial void OnSelectedLevelFilterIndexChanged( int value ) => ApplyFilters();

	public void Attach()
	{
		if( !_attached )
		{
			_session.MessageLogged += OnSessionMessageLogged;
			_session.MessagesCleared += OnSessionMessagesCleared;
			_attached = true;
		}
		SyncFromSession();
	}

	public void Detach()
	{
		if( !_attached )
			return;
		_session.MessageLogged -= OnSessionMessageLogged;
		_session.MessagesCleared -= OnSessionMessagesCleared;
		_attached = false;
	}

	public void Dispose() => Detach();

	public void RefreshConnectionSummary()
	{
		ConnectionTargetText = FormatConnectionTarget();
		OnPropertyChanged( nameof( IsLoggedIn ) );
		OnPropertyChanged( nameof( ConnectionBadgeText ) );
		InspectEnvironmentAndSettings( forceLog: false );
	}

	[RelayCommand]
	private void CheckStatus()
	{
		ConnectionTargetText = FormatConnectionTarget();
		InspectEnvironmentAndSettings( forceLog: true );
	}

	[RelayCommand]
	private void ClearStatus()
	{
		_session.ClearMessages();
		HasBanner = false;
		ShowOpenConfigurationsAction = false;
	}

	[RelayCommand]
	private async Task TestConnectionAsync()
	{
		if( IsBusy )
			return;
		IsTestingConnection = true;
		LoginStatusText = "Testing connection to Odoo…";
		try
		{
			OdooDefaults odoo = OdooDefaults.Instance;
			string host = odoo.OdooAddress?.Trim() ?? string.Empty;
			string portText = odoo.OdooPort?.Trim() ?? string.Empty;
			bool useHttps = odoo.OdooUseHttps;
			string url = !string.IsNullOrEmpty( odoo.OdooUrl )
				? odoo.OdooUrl
				: OdooEndpoint.BuildUrl( host, portText, useHttps );

			if( string.IsNullOrEmpty( host ) || OdooConnection.ResolveEndpoint( host, portText, useHttps ) is not var (resolvedHost, resolvedPort) )
			{
				LogSession( StatusMessage.ERROR, ProfileStatusCategory.Configuration, "Cannot test connection: Odoo server address or port is missing or invalid.", updateBanner: true );
				return;
			}

			if( OdooEndpoint.InsecureReason( url, allowPlainHttp: !useHttps ) is { } insecure )
			{
				LogSession( StatusMessage.ERROR, ProfileStatusCategory.Connection, insecure, updateBanner: true );
				return;
			}

			if( !useHttps && OdooEndpoint.InsecureReason( url, allowPlainHttp: false ) is not null )
				LogSession( StatusMessage.WARNING, ProfileStatusCategory.Connection, $"Plain HTTP enabled for {url}; credentials are not encrypted." );
			else
				LogSession( StatusMessage.INFO, ProfileStatusCategory.Connection, $"Endpoint policy valid: {url}." );

			LoginStatusText = $"Resolving host '{resolvedHost}'…";
			if( !await OdooConnection.CorrectOdooAddress() )
			{
				LogSession( StatusMessage.ERROR, ProfileStatusCategory.Connection, $"Host '{resolvedHost}' is unreachable or could not be resolved in DNS.", updateBanner: true );
				return;
			}

			LoginStatusText = $"Checking TCP port {resolvedPort}…";
			if( !await OdooConnection.CorrectOdooPort() )
			{
				LogSession( StatusMessage.ERROR, ProfileStatusCategory.Connection, $"TCP port {resolvedPort} on '{resolvedHost}' refused the connection or timed out.", updateBanner: true );
				return;
			}

			LoginStatusText = "Probing Odoo JSON-RPC service…";
			Stopwatch sw = Stopwatch.StartNew();
			Result<JsonElement> probe = await OdooRpc.CallAsync( "common", "version", [], timeoutMs: 5000 );
			sw.Stop();
			if( probe.Ok )
			{
				string? serverVersion = probe.Value.ValueKind == JsonValueKind.Object && probe.Value.TryGetProperty( "server_version", out JsonElement ver )
					? ver.GetString()
					: null;
				string versionLabel = serverVersion is { Length: > 0 } ? $" (Odoo {serverVersion})" : string.Empty;
				LogSession( StatusMessage.SUCCESS, ProfileStatusCategory.Connection, $"Odoo JSON-RPC responded at {url}{versionLabel} in {sw.ElapsedMilliseconds} ms.", updateBanner: true );
			}
			else
			{
				LogSession( StatusMessage.ERROR, ProfileStatusCategory.Connection, $"TCP port {resolvedPort} opened, but Odoo JSON-RPC probe failed: {probe.Error ?? "unknown error"}", updateBanner: true );
			}
		}
		catch( Exception ex )
		{
			LogSession( StatusMessage.ERROR, ProfileStatusCategory.Connection, $"Connection test failed: {ex.Message}", updateBanner: true );
		}
		finally
		{
			LoginStatusText = string.Empty;
			IsTestingConnection = false;
		}
	}

	[RelayCommand]
	private void Logout()
	{
		string target = FormatConnectionTarget();
		IsLoggedIn = false;
		OdooDefaults.Instance.OdooId = 0;
		_accountChanged?.Invoke();
		LogSession( StatusMessage.INFO, ProfileStatusCategory.Login, $"Signed out of Odoo session ({target}).", updateBanner: true );
	}

	[RelayCommand]
	private async Task LoginAsync()
	{
		if( IsBusy )
			return;
		IsLoggingIn = true;
		LoginStatusText = "Signing in to Odoo…";
		bool signedIn = false;
		try
		{
			signedIn = await AbleToLoginAsync();
			IsLoggedIn = signedIn;
			if( signedIn )
			{
				OdooDefaults odoo = OdooDefaults.Instance;
				if( _session.LoggedInAs( $"{odoo.OdooUrl}|{odoo.OdooDb}|{odoo.OdooUser}|{HackDefaults.Instance.PwaPathAbsolute}" ) )
				{
					_accountChanged?.Invoke();
					LogSession( StatusMessage.INFO, ProfileStatusCategory.Login, $"Switched active account to '{odoo.OdooUser}' on '{odoo.OdooDb}'; cached workspace cleared." );
				}
				if( odoo.SaveCredentials() )
					LogSession( StatusMessage.INFO, ProfileStatusCategory.Settings, $"Credentials saved to keyring ({odoo.OdooCredentialTarget ?? PdmConstants.DefaultOdooCredentials})." );
				else
					LogSession( StatusMessage.WARNING, ProfileStatusCategory.Settings, "Credentials were not stored in the system keyring." );

				LogSession( StatusMessage.SUCCESS, ProfileStatusCategory.Login, $"Signed in as '{odoo.OdooUser}' (uid {odoo.OdooId}) on '{odoo.OdooDb}'.", updateBanner: true );
				LoginStatusText = "Opening File Manager…";
			}
		}
		finally
		{
			ConnectionTargetText = FormatConnectionTarget();
			OnPropertyChanged( nameof( IsLoggedIn ) );
			OnPropertyChanged( nameof( ConnectionBadgeText ) );
			if( !signedIn )
				LoginStatusText = string.Empty;
			IsLoggingIn = false;
		}
		if( signedIn )
		{
			_navigation.NavigateTo( NavigatePageMenu.HackFileManager );
			LoginStatusText = string.Empty;
		}
	}

	[RelayCommand]
	private void OpenSettings() => _navigation.NavigateTo( NavigatePageMenu.Configuration );

	[RelayCommand]
	private async Task LoadProfileAsync()
	{
		if( await _pickers.PickOpenFileAsync( ProfileExtension ) is not { } path )
			return;
		string fileName = Path.GetFileName( path );
		SavedProfileData? profile = null;
		string? readError = null;
		try
		{
			profile = await ProfileStore.ReadAsync( path );
			readError ??= profile is null ? "profile JSON evaluated to null" : null;
		}
		catch( Exception ex ) when( ex is JsonException or IOException or UnauthorizedAccessException )
		{
			readError = ex.Message;
		}
		if( profile is null )
		{
			LogSession( StatusMessage.ERROR, ProfileStatusCategory.Configuration, $"Unable to load profile '{fileName}': {readError ?? "invalid JSON format"}.", updateBanner: true );
			await _dialogs.ShowMessageAsync( "Unable to read JSON profile file.", "Invalid Profile", MessageBoxButtons.OK, MessageBoxIcon.Error );
			return;
		}
		ProfileStore.Apply( profile );
		LogSession( StatusMessage.INFO, ProfileStatusCategory.Configuration, $"Loaded profile '{fileName}' ({FormatConnectionTarget()}).", updateBanner: true );
		if( string.IsNullOrEmpty( profile.Password ) )
			LogSession( StatusMessage.WARNING, ProfileStatusCategory.Configuration, $"Profile '{fileName}' has no password; existing keyring or Configurations credentials will be used." );
		RefreshConnectionSummary();
	}

	[RelayCommand] private Task SaveProfileAsync() => SaveAsync( ProfileStore.Current( includePassword: true ), "profile (with password)" );
	[RelayCommand] private Task SaveProfileWithoutPasswordAsync() => SaveAsync( ProfileStore.Current( includePassword: false ), "profile (without password)" );
	[RelayCommand] private Task CreateTemplateAsync() => SaveAsync( ProfileStore.Template, "template profile" );

	private async Task SaveAsync( SavedProfileData profile, string label )
	{
		if( await _pickers.PickSaveFileAsync( "JSON file", ProfileExtension, "hack-odoo" ) is not { } path )
			return;
		string fileName = Path.GetFileName( path );
		try
		{
			await ProfileStore.WriteAsync( path, profile );
			LogSession( StatusMessage.INFO, ProfileStatusCategory.Configuration, $"Saved {label}: '{fileName}'.", updateBanner: true );
		}
		catch( Exception ex ) when( ex is IOException or UnauthorizedAccessException or JsonException )
		{
			LogSession( StatusMessage.ERROR, ProfileStatusCategory.Configuration, $"Failed saving {label} '{fileName}': {ex.Message}", updateBanner: true );
			await _dialogs.ShowMessageAsync( $"Unable to save {label}: {ex.Message}", "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error );
		}
	}

	public async Task<bool> AbleToLoginAsync()
	{
		try
		{
			List<(string Category, string Message)> errors = [];
			Progress<string> stageProgress = new( message => UiDispatcher.Post( () => LoginStatusText = message ) );
			LoginStatusText = "Signing in to Odoo…";
			LogSession( StatusMessage.INFO, ProfileStatusCategory.Login, $"Attempting login ({FormatConnectionTarget()})…" );

			try
			{
				await OdooDefaults.Instance.InitializeAsync( stageProgress );
			}
			catch( Exception ex ) when( OdooDefaults.Instance.OdooId == 0 )
			{
				errors.Add( ( ClassifyErrorCategory( ex.Message ), ex.Message ) );
			}

			if( OdooConnection.CorrectUserId() == 1 )
			{
				LoginStatusText = "Checking client compatibility…";
				if( await HackUpdater.EnsureUpdatedAsync( true ) )
				{
					_session.VersionMismatchMessage = null;
					OdooDefaults odoo = OdooDefaults.Instance;
					LogSession( StatusMessage.INFO, ProfileStatusCategory.Connection,
						$"Synced server metadata (node: {odoo.MyNode?.name ?? "(none)"}, {odoo.HpTypes?.Length ?? 0} file types, {odoo.HpProperties?.Length ?? 0} properties)." );
					return true;
				}
				OdooDefaults.Instance.OdooId = 0;
				_session.IsLoggedIn = false;
				string required = HackUpdater.RequiredVersionRequirement ?? "(unspecified)";
				string mismatchMsg = $"Access denied: HackPDM {ClientUpdate.CurrentText} does not satisfy the server's required version '{required}'. Update HackPDM to continue.";
				_session.VersionMismatchMessage = mismatchMsg;
				errors.Add( ( ProfileStatusCategory.Login, mismatchMsg ) );
			}
			else
			{
				LoginStatusText = "Checking server reachability…";
				if( !await OdooConnection.CorrectOdooAddress() )
					errors.Add( ( ProfileStatusCategory.Connection, "Invalid Odoo address or unreachable host." ) );
				else if( !await OdooConnection.CorrectOdooPort() )
					errors.Add( ( ProfileStatusCategory.Connection, "Invalid Odoo port or server is down." ) );
				else if( OdooDefaults.Instance.LoginError is { Length: > 0 } loginErr )
					errors.Add( ( ClassifyErrorCategory( loginErr ), loginErr ) );
				else if( errors.Count == 0 )
					errors.Add( ( ProfileStatusCategory.Login, "Invalid Odoo credentials." ) );
			}

			foreach( var (category, message) in errors.DistinctBy( e => $"{e.Category}:{e.Message}", StringComparer.OrdinalIgnoreCase ) )
				LogSession( StatusMessage.ERROR, category, message, updateBanner: true );
			return false;
		}
		catch( Exception ex )
		{
			HackLogger.Error( "Login", $"Unexpected login error: {ex.Message}", ex );
			LogSession( StatusMessage.ERROR, ProfileStatusCategory.Login, $"Unexpected login error: {ex.Message}", updateBanner: true );
			return false;
		}
	}

	private void InspectEnvironmentAndSettings( bool forceLog )
	{
		List<(StatusMessage Status, string Category, string Message)> findings = [];
		OdooDefaults odoo = OdooDefaults.Instance;
		string host = odoo.OdooAddress?.Trim() ?? string.Empty;
		string port = odoo.OdooPort?.Trim() ?? string.Empty;
		string db = odoo.OdooDb?.Trim() ?? string.Empty;
		string user = odoo.OdooUser?.Trim() ?? string.Empty;
		string pass = odoo.OdooPass ?? string.Empty;
		bool useHttps = odoo.OdooUseHttps;
		string url = odoo.OdooUrl is { Length: > 0 } existingUrl ? existingUrl : OdooEndpoint.BuildUrl( host, port, useHttps );

		if( host.Length == 0 )
			findings.Add( ( StatusMessage.ERROR, ProfileStatusCategory.Configuration, "Odoo server address is not configured." ) );
		else if( port.Length > 0 && ( !int.TryParse( port, out int parsedPort ) || parsedPort is < 1 or > 65535 ) )
			findings.Add( ( StatusMessage.ERROR, ProfileStatusCategory.Configuration, $"Odoo port '{port}' is invalid (expected 1–65535)." ) );

		if( db.Length == 0 )
			findings.Add( ( StatusMessage.ERROR, ProfileStatusCategory.Configuration, "Odoo database name is not configured." ) );
		if( user.Length == 0 || pass.Length == 0 )
			findings.Add( ( StatusMessage.WARNING, ProfileStatusCategory.Configuration, "Odoo username or password is not set." ) );
		if( host.Length > 0 && db.Length > 0 && user.Length > 0 )
			findings.Add( ( StatusMessage.INFO, ProfileStatusCategory.Configuration, $"Odoo configuration target: {user} @ {db} ({url})." ) );

		string pwa = HackDefaults.Instance.PwaPathAbsolute.Trim();
		if( pwa.Length == 0 )
			findings.Add( ( StatusMessage.WARNING, ProfileStatusCategory.Configuration, "Local workspace folder (PWA) is not configured." ) );
		else if( !Directory.Exists( pwa ) )
			findings.Add( ( StatusMessage.WARNING, ProfileStatusCategory.Configuration, $"Local workspace folder does not exist yet: '{pwa}'." ) );
		else
			findings.Add( ( StatusMessage.INFO, ProfileStatusCategory.Configuration, $"Local workspace folder ready: '{pwa}'." ) );

		if( host.Length > 0 )
		{
			if( OdooEndpoint.InsecureReason( url, allowPlainHttp: !useHttps ) is { } blocked )
				findings.Add( ( StatusMessage.ERROR, ProfileStatusCategory.Connection, blocked ) );
			else if( !useHttps && OdooEndpoint.InsecureReason( url, allowPlainHttp: false ) is not null )
				findings.Add( ( StatusMessage.WARNING, ProfileStatusCategory.Connection, $"Plain HTTP enabled for {url}; passwords are not encrypted." ) );
			else
				findings.Add( ( StatusMessage.INFO, ProfileStatusCategory.Connection, $"Connection endpoint ready: {url}." ) );
		}

		if( _inspectSettings?.Invoke() is { } settingsItems )
			findings.AddRange( settingsItems );

		string fingerprint = string.Join( "|", findings.Select( f => $"{(int)f.Status}:{f.Category}:{f.Message}" ) );
		if( forceLog || !string.Equals( _session.LastReadinessFingerprint, fingerprint, StringComparison.Ordinal ) )
		{
			_session.LastReadinessFingerprint = fingerprint;
			foreach( var item in findings )
				_session.AddStatus( item.Status, item.Category, item.Message );
		}

		if( findings.FirstOrDefault( f => f.Status == StatusMessage.ERROR ) is { Message.Length: > 0 } err )
			SetBanner( StatusMessage.ERROR, err.Category, err.Message );
		else if( findings.FirstOrDefault( f => f.Status == StatusMessage.WARNING ) is { Message.Length: > 0 } warn )
			SetBanner( StatusMessage.WARNING, warn.Category, warn.Message );
		else if( _session.LastErrors.LastOrDefault() is { } latest )
			SetBanner( latest.Status, latest.Category, latest.Message ?? string.Empty );
		else if( IsLoggedIn )
			SetBanner( StatusMessage.SUCCESS, ProfileStatusCategory.Login, $"Connected as {ConnectionTargetText}." );
	}

	private static string FormatConnectionTarget()
	{
		OdooDefaults odoo = OdooDefaults.Instance;
		string host = odoo.OdooAddress?.Trim() ?? string.Empty;
		if( host.Length == 0 )
			return "No Odoo server configured yet";
		string url = odoo.OdooUrl is { Length: > 0 } u ? u : OdooEndpoint.BuildUrl( host, odoo.OdooPort, odoo.OdooUseHttps );
		string user = odoo.OdooUser is { Length: > 0 } usr ? usr : "(no user)";
		string db = odoo.OdooDb is { Length: > 0 } d ? d : "(no db)";
		return $"{user} @ {db} — {url}";
	}

	private static string ClassifyErrorCategory( string message )
	{
		string lower = message.ToLowerInvariant();
		if( lower.Contains( "must be set", StringComparison.Ordinal )
			|| lower.Contains( "database", StringComparison.Ordinal )
			|| lower.Contains( "address is not set", StringComparison.Ordinal )
			|| lower.Contains( "not a valid server", StringComparison.Ordinal ) )
			return ProfileStatusCategory.Configuration;

		if( lower.Contains( "unencrypted", StringComparison.Ordinal )
			|| lower.Contains( "timeout", StringComparison.Ordinal )
			|| lower.Contains( "timed out", StringComparison.Ordinal )
			|| lower.Contains( "refused", StringComparison.Ordinal )
			|| lower.Contains( "unreachable", StringComparison.Ordinal )
			|| lower.Contains( "ssl", StringComparison.Ordinal )
			|| lower.Contains( "tls", StringComparison.Ordinal )
			|| lower.Contains( "connect", StringComparison.Ordinal ) )
			return ProfileStatusCategory.Connection;

		return ProfileStatusCategory.Login;
	}

	private static void Dispatch( Action action )
	{
		if( UiDispatcher.Current.HasThreadAccess )
			action();
		else
			UiDispatcher.Post( action );
	}

	private void LogSession( StatusMessage status, string category, string message, bool updateBanner = false )
	{
		BasicStatusMessage entry = _session.AddStatus( status, category, message );
		if( updateBanner )
			Dispatch( () => SetBanner( entry.Status, entry.Category, entry.Message ?? string.Empty ) );
	}

	private void SetBanner( StatusMessage status, string category, string message )
	{
		if( string.IsNullOrWhiteSpace( message ) )
		{
			HasBanner = false;
			ShowOpenConfigurationsAction = false;
			return;
		}
		BannerStatus = status;
		string levelWord = status switch
		{
			StatusMessage.ERROR => "Error",
			StatusMessage.WARNING => "Warning",
			StatusMessage.SUCCESS => "Ready",
			_ => "Status",
		};
		BannerTitle = string.IsNullOrEmpty( category ) ? levelWord : $"{category} {levelWord}";
		BannerMessage = message;
		ShowOpenConfigurationsAction = status is StatusMessage.ERROR or StatusMessage.WARNING
			&& category is ProfileStatusCategory.Configuration or ProfileStatusCategory.Connection;
		HasBanner = true;
	}

	private void OnSessionMessageLogged( BasicStatusMessage entry )
		=> Dispatch( () =>
		{
			OStatus.Add( entry );
			if( MatchesFilters( entry ) )
				FilteredStatus.Insert( 0, entry );
			if( entry.Status is StatusMessage.ERROR or StatusMessage.WARNING or StatusMessage.SUCCESS )
				SetBanner( entry.Status, entry.Category, entry.Message ?? string.Empty );
			UpdateCountsAndHints();
		} );

	private void OnSessionMessagesCleared()
		=> Dispatch( () =>
		{
			OStatus.Clear();
			FilteredStatus.Clear();
			UpdateCountsAndHints();
		} );

	private void SyncFromSession()
	{
		OStatus.Clear();
		foreach( BasicStatusMessage item in _session.LastErrors )
			OStatus.Add( item );
		ApplyFilters();
	}

	private void ApplyFilters()
	{
		FilteredStatus.Clear();
		for( int i = OStatus.Count - 1; i >= 0; i-- )
		{
			if( MatchesFilters( OStatus[ i ] ) )
				FilteredStatus.Add( OStatus[ i ] );
		}
		UpdateCountsAndHints();
	}

	private bool MatchesFilters( BasicStatusMessage item )
	{
		if( SelectedCategoryFilterIndex > 0 && SelectedCategoryFilterIndex < CategoryFilterOptions.Length )
		{
			if( !string.Equals( item.Category, CategoryFilterOptions[ SelectedCategoryFilterIndex ], StringComparison.OrdinalIgnoreCase ) )
				return false;
		}

		return SelectedLevelFilterIndex switch
		{
			1 => item.Status is StatusMessage.ERROR or StatusMessage.WARNING,
			2 => item.Status is StatusMessage.ERROR,
			3 => item.Status is StatusMessage.INFO or StatusMessage.SUCCESS or StatusMessage.FOUND,
			_ => true,
		};
	}

	private void UpdateCountsAndHints()
	{
		int errors = 0, warnings = 0, infos = 0;
		foreach( BasicStatusMessage item in OStatus )
		{
			switch( item.Status )
			{
				case StatusMessage.ERROR: errors++; break;
				case StatusMessage.WARNING: warnings++; break;
				default: infos++; break;
			}
		}

		if( OStatus.Count == 0 )
		{
			StatusCountsText = "No status messages";
			EmptyHintTitle = "Ready to connect";
			EmptyHintSubtitle = "Click Odoo Login or Test Connection to record login, connection, config, and settings status.";
		}
		else
		{
			List<string> parts = [];
			if( errors > 0 ) parts.Add( errors == 1 ? "1 error" : $"{errors} errors" );
			if( warnings > 0 ) parts.Add( warnings == 1 ? "1 warning" : $"{warnings} warnings" );
			if( infos > 0 ) parts.Add( $"{infos} info" );
			StatusCountsText = string.Join( " · ", parts );
			EmptyHintTitle = FilteredStatus.Count == 0 ? "No matching status messages" : "Ready to connect";
			EmptyHintSubtitle = FilteredStatus.Count == 0
				? "Change the area or severity filter above to view other recorded messages."
				: "Click Odoo Login below to authenticate and sync workspace metadata.";
		}
	}
}
