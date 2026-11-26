using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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
using HackPDM.Shell;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace HackPDM.Login;

[Bindable]
public sealed partial class ProfileManagerViewModel : ObservableObject, IDisposable
{
	private const string ProfileExtension = ".json";

	private sealed record EnvironmentSnapshot(
		string ConnectionTarget,
		List<(StatusMessage Status, string Category, string Message)> Findings );

	private static readonly string[] SharedCategoryFilterOptions =
	[
		"All Areas",
		ProfileStatusCategory.Login,
		ProfileStatusCategory.Connection,
		ProfileStatusCategory.Configuration,
		ProfileStatusCategory.Settings,
	];

	private static readonly string[] SharedLevelFilterOptions =
	[
		"All Levels",
		"Errors & Warnings",
		"Errors Only",
		"Info & Success",
	];

	private readonly LoginSession _session;
	private readonly IDialogService _dialogs;
	private readonly IFilePickerService _pickers;
	private readonly INavigationService _navigation;
	private readonly Func<Task> _accountChanged;
	private readonly Func<IReadOnlyList<(StatusMessage Status, string Category, string Message)>> _inspectSettings;
	private readonly SearchOdooViewModel _search;
	private readonly Action? _onAuthStateChanged;
	private CancellationTokenSource? _operationCts;
	private CancellationTokenSource? _inspectCts;
	private Task? _inspectTask;
	private bool _attached;
	private bool _batchingSessionLogs;
	private int _errorCount;
	private int _warningCount;
	private int _infoCount;

	public ProfileManagerViewModel(
		LoginSession session,
		IDialogService dialogs,
		IFilePickerService pickers,
		INavigationService navigation,
		Func<Task> accountChanged,
		Func<IReadOnlyList<(StatusMessage Status, string Category, string Message)>> inspectSettings,
		SearchOdooViewModel search,
		Action? onAuthStateChanged = null )
	{
		_session = session;
		_dialogs = dialogs;
		_pickers = pickers;
		_navigation = navigation;
		_accountChanged = accountChanged;
		_inspectSettings = inspectSettings;
		_search = search;
		_onAuthStateChanged = onAuthStateChanged;
		IsLoggedIn = _session.IsLoggedIn;
		Attach();
	}

	public RangeObservableCollection<BasicStatusMessage> OStatus { get; } = [];
	public RangeObservableCollection<BasicStatusMessage> FilteredStatus { get; } = [];

	public string[] CategoryFilterOptions => SharedCategoryFilterOptions;
	public string[] LevelFilterOptions => SharedLevelFilterOptions;

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( ConnectionBadgeText ) )]
	public partial bool IsLoggedIn { get; set; }

	partial void OnIsLoggedInChanged( bool value ) => _session.IsLoggedIn = value;

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
	[ObservableProperty] public partial string ConnectionTargetText { get; set; } = string.Empty;
	[ObservableProperty] public partial int SelectedCategoryFilterIndex { get; set; }
	[ObservableProperty] public partial int SelectedLevelFilterIndex { get; set; }
	[ObservableProperty] public partial bool HasBanner { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( BannerSeverity ) )]
	public partial StatusMessage BannerStatus { get; set; } = StatusMessage.INFO;

	[ObservableProperty] public partial string BannerTitle { get; set; } = string.Empty;
	[ObservableProperty] public partial string BannerMessage { get; set; } = string.Empty;
	[ObservableProperty] public partial bool ShowOpenConfigurationsAction { get; set; }
	[ObservableProperty] public partial string StatusCountsText { get; set; } = "No messages";
	[ObservableProperty] public partial string EmptyHintTitle { get; set; } = "Ready to connect";
	[ObservableProperty] public partial string EmptyHintSubtitle { get; set; } = "Click Odoo Login below to authenticate and sync workspace metadata.";

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( HasSelectedStatusMessage ), nameof( SelectedStatusMessageText ) )]
	public partial BasicStatusMessage? SelectedStatusMessage { get; set; }

	public bool IsBusy => IsLoggingIn || IsTestingConnection;
	public bool IsIdle => !IsBusy;
	public string LoginButtonText => IsLoggingIn ? "Signing in…" : "Odoo Login";
	public string TestConnectionButtonText => IsTestingConnection ? "Testing…" : "Test Connection";
	public string ConnectionBadgeText => IsLoggedIn ? "Signed In" : "Not Signed In";
	public bool IsFilteredStatusEmpty => FilteredStatus.Count == 0;
	public bool HasSelectedStatusMessage => SelectedStatusMessage is not null;
	public string SelectedStatusMessageText => SelectedStatusMessage is { } m
		? $"[{m.TimestampText}] [{m.Status}] {m.Category}: {m.Message}"
		: string.Empty;
	public InfoBarSeverity BannerSeverity => BannerStatus switch
	{
		StatusMessage.ERROR => InfoBarSeverity.Error,
		StatusMessage.WARNING => InfoBarSeverity.Warning,
		StatusMessage.SUCCESS => InfoBarSeverity.Success,
		_ => InfoBarSeverity.Informational,
	};

	partial void OnSelectedCategoryFilterIndexChanged( int value ) => ApplyFilters();
	partial void OnSelectedLevelFilterIndexChanged( int value ) => ApplyFilters();

	public void Attach()
	{
		if( _attached )
			return;
		_session.MessageLogged += OnSessionMessageLogged;
		_session.MessagesCleared += OnSessionMessagesCleared;
		_attached = true;
		SyncFromSession();
	}

	public void Detach()
	{
		CtsHelper.CancelAndDispose( ref _operationCts );
		CtsHelper.CancelAndDispose( ref _inspectCts );
		if( !_attached )
			return;
		_session.MessageLogged -= OnSessionMessageLogged;
		_session.MessagesCleared -= OnSessionMessagesCleared;
		_attached = false;
	}

	public void Dispose() => Detach();

	public void RefreshConnectionSummary() => _ = RefreshConnectionSummaryAsync();

	public Task RefreshConnectionSummaryAsync()
	{
		IsLoggedIn = _session.IsLoggedIn;
		return StartInspectAsync( forceLog: false );
	}

	[RelayCommand]
	private Task CheckStatusAsync() => StartInspectAsync( forceLog: true );

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
		CancellationToken token = CtsHelper.Replace( ref _operationCts ).Token;
		IsTestingConnection = true;
		LoginStatusText = "Testing connection to Odoo…";
		try
		{
			await WaitPendingInspectAsync( token );
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
			if( !await OdooConnection.CorrectOdooAddress().WaitAsync( token ) )
			{
				LogSession( StatusMessage.ERROR, ProfileStatusCategory.Connection, $"Host '{resolvedHost}' is unreachable or could not be resolved in DNS.", updateBanner: true );
				return;
			}

			LoginStatusText = $"Checking TCP port {resolvedPort}…";
			if( !await OdooConnection.CorrectOdooPort().WaitAsync( token ) )
			{
				LogSession( StatusMessage.ERROR, ProfileStatusCategory.Connection, $"TCP port {resolvedPort} on '{resolvedHost}' refused the connection or timed out.", updateBanner: true );
				return;
			}

			LoginStatusText = "Probing Odoo JSON-RPC service…";
			Stopwatch sw = Stopwatch.StartNew();
			Result<JsonElement> probe = await OdooRpc.CallAsync( "common", "version", [], timeoutMs: 5000, token: token );
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
		catch( OperationCanceledException ) when( token.IsCancellationRequested )
		{
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
	private async Task LogoutAsync()
	{
		string target = await Task.Run( FormatConnectionTarget );
		IsLoggedIn = false;
		_session.VersionMismatchMessage = null;
		OdooDefaults.Instance.OdooId = 0;
		OdooDefaults.Instance.Lookups = OdooLookupCache.Empty;
		await _accountChanged();
		_onAuthStateChanged?.Invoke();
		_search.ResetForSessionChange();
		LogSession( StatusMessage.INFO, ProfileStatusCategory.Login, $"Signed out of Odoo session ({target}).", updateBanner: true );
	}

	[RelayCommand]
	private async Task LoginAsync()
	{
		if( IsBusy )
			return;
		CancellationToken token = CtsHelper.Replace( ref _operationCts ).Token;
		IsLoggingIn = true;
		LoginStatusText = "Signing in to Odoo…";
		bool signedIn = false;
		try
		{
			await WaitPendingInspectAsync( token );
			signedIn = await AbleToLoginAsync( token );
			IsLoggedIn = signedIn;
			if( signedIn )
			{
				OdooDefaults odoo = OdooDefaults.Instance;
				string accountKey = $"{odoo.OdooUrl}|{odoo.OdooDb}|{odoo.OdooUser}|{HackDefaults.Instance.PwaPathAbsolute}";
				if( _session.LoggedInAs( accountKey ) )
				{
					await _accountChanged();
					_search.ResetForSessionChange();
					LogSession( StatusMessage.INFO, ProfileStatusCategory.Login, $"Switched active account to '{odoo.OdooUser}' on '{odoo.OdooDb}'; cached workspace cleared." );
				}
				_onAuthStateChanged?.Invoke();
				_search.LoadLookups();
				bool savedCredentials = await Task.Run( () => odoo.SaveCredentials(), token );
				if( savedCredentials )
					LogSession( StatusMessage.INFO, ProfileStatusCategory.Settings, $"Credentials saved to keyring ({odoo.OdooCredentialTarget ?? PdmConstants.DefaultOdooCredentials})." );
				else
					LogSession( StatusMessage.WARNING, ProfileStatusCategory.Settings, "Credentials were not stored in the system keyring." );

				LogSession( StatusMessage.SUCCESS, ProfileStatusCategory.Login, $"Signed in as '{odoo.OdooUser}' (uid {odoo.OdooId}) on '{odoo.OdooDb}'.", updateBanner: true );
				LoginStatusText = "Opening File Manager…";
			}
		}
		catch( OperationCanceledException ) when( token.IsCancellationRequested )
		{
			signedIn = false;
			IsLoggedIn = false;
		}
		catch( Exception ex )
		{
			signedIn = false;
			IsLoggedIn = false;
			HackLogger.Error( "Login", $"Post-login setup failed: {ex.Message}", ex );
			LogSession( StatusMessage.ERROR, ProfileStatusCategory.Login, $"Sign-in failed: {ex.Message}", updateBanner: true );
		}
		finally
		{
			ConnectionTargetText = await Task.Run( FormatConnectionTarget );
			if( !signedIn )
				LoginStatusText = string.Empty;
			IsLoggingIn = false;
		}
		if( signedIn && !token.IsCancellationRequested )
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
		CancellationToken token = CtsHelper.Replace( ref _operationCts ).Token;
		string fileName = Path.GetFileName( path );
		SavedProfileData? profile = null;
		string? readError = null;
		try
		{
			profile = await ProfileStore.ReadAsync( path ).WaitAsync( token );
			readError ??= profile is null ? "profile JSON evaluated to null" : null;
		}
		catch( OperationCanceledException ) when( token.IsCancellationRequested )
		{
			return;
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

		bool targetChanged;
		try
		{
			targetChanged = await Task.Run( () =>
			{
				OdooDefaults defaults = OdooDefaults.Instance;
				string? previousUrl = defaults.OdooUrl;
				string? previousDb = defaults.OdooDb;
				string? previousUser = defaults.OdooUser;
				string? previousPass = defaults.OdooPass;
				string? previousTarget = defaults.OdooCredentialTarget;
				string previousPwa = HackDefaults.Instance.PwaPathAbsolute;

				ProfileStore.Apply( profile );

				bool pwaChanged = !string.Equals( previousPwa, HackDefaults.Instance.PwaPathAbsolute, PwaPaths.PathComparison );
				return pwaChanged
					|| !string.Equals( previousUrl, defaults.OdooUrl, StringComparison.OrdinalIgnoreCase )
					|| !string.Equals( previousDb, defaults.OdooDb, StringComparison.Ordinal )
					|| !string.Equals( previousUser, defaults.OdooUser, StringComparison.Ordinal )
					|| !string.Equals( previousPass, defaults.OdooPass, StringComparison.Ordinal )
					|| !string.Equals( previousTarget, defaults.OdooCredentialTarget, StringComparison.Ordinal );
			}, token );
		}
		catch( OperationCanceledException ) when( token.IsCancellationRequested )
		{
			return;
		}

		IsLoggedIn = false;
		_session.VersionMismatchMessage = null;
		OdooDefaults.Instance.Lookups = OdooLookupCache.Empty;
		if( targetChanged )
		{
			await _accountChanged();
			_search.ResetForSessionChange();
		}
		else
		{
			_search.LoadLookups();
		}

		string targetSummary = await Task.Run( FormatConnectionTarget, token );
		LogSession( StatusMessage.INFO, ProfileStatusCategory.Configuration, $"Loaded profile '{fileName}' ({targetSummary}).", updateBanner: true );
		if( string.IsNullOrEmpty( profile.Password ) )
			LogSession( StatusMessage.WARNING, ProfileStatusCategory.Configuration, $"Profile '{fileName}' has no password; existing keyring or Configurations credentials will be used." );
		await RefreshConnectionSummaryAsync();
	}

	[RelayCommand] private Task SaveProfileAsync() => SaveAsync( () => ProfileStore.Current( includePassword: true ), "profile (with password)" );
	[RelayCommand] private Task SaveProfileWithoutPasswordAsync() => SaveAsync( () => ProfileStore.Current( includePassword: false ), "profile (without password)" );
	[RelayCommand] private Task CreateTemplateAsync() => SaveAsync( static () => ProfileStore.Template, "template profile" );

	private async Task SaveAsync( Func<SavedProfileData> buildProfile, string label )
	{
		if( await _pickers.PickSaveFileAsync( "JSON file", ProfileExtension, "hack-odoo" ) is not { } path )
			return;
		string fileName = Path.GetFileName( path );
		try
		{
			SavedProfileData profile = await Task.Run( buildProfile );
			await ProfileStore.WriteAsync( path, profile );
			LogSession( StatusMessage.INFO, ProfileStatusCategory.Configuration, $"Saved {label}: '{fileName}'.", updateBanner: true );
		}
		catch( Exception ex ) when( ex is IOException or UnauthorizedAccessException or JsonException )
		{
			LogSession( StatusMessage.ERROR, ProfileStatusCategory.Configuration, $"Failed saving {label} '{fileName}': {ex.Message}", updateBanner: true );
			await _dialogs.ShowMessageAsync( $"Unable to save {label}: {ex.Message}", "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error );
		}
	}

	public async Task<bool> AbleToLoginAsync( CancellationToken token = default )
	{
		try
		{
			token.ThrowIfCancellationRequested();
			List<(string Category, string Message)> errors = [];
			IProgress<string> stageProgress = new UiThreadProgress<string>( message => LoginStatusText = message );
			LoginStatusText = "Signing in to Odoo…";
			string targetText = await Task.Run( FormatConnectionTarget, token );
			LogSession( StatusMessage.INFO, ProfileStatusCategory.Login, $"Attempting login ({targetText})…" );

			try
			{
				await Task.Run( () => OdooDefaults.Instance.InitializeAsync( stageProgress ), token ).WaitAsync( token );
			}
			catch( OperationCanceledException ) when( token.IsCancellationRequested )
			{
				throw;
			}
			catch( Exception ex )
			{
				OdooDefaults.Instance.OdooId = 0;
				errors.Add( ( ClassifyErrorCategory( ex.Message ), ex.Message ) );
			}

			token.ThrowIfCancellationRequested();
			if( OdooConnection.CorrectUserId() == 1 )
			{
				LoginStatusText = "Checking client compatibility…";
				if( await HackUpdater.EnsureUpdatedAsync( showOutdatedUpdater: true, token: token ) )
				{
					_session.VersionMismatchMessage = null;
					OdooDefaults odoo = OdooDefaults.Instance;
					LogSession( StatusMessage.INFO, ProfileStatusCategory.Connection,
						$"Synced server metadata (node: {odoo.MyNode?.name ?? "(none)"}, {odoo.HpTypes?.Length ?? 0} file types, {odoo.HpProperties?.Length ?? 0} properties)." );
					return true;
				}
				OdooDefaults.Instance.OdooId = 0;
				IsLoggedIn = false;
				string required = HackUpdater.RequiredVersionRequirement ?? "(unspecified)";
				string mismatchMsg = $"Access denied: HackPDM {ClientUpdate.CurrentText} does not satisfy the server's required version '{required}'. Update HackPDM to continue.";
				_session.VersionMismatchMessage = mismatchMsg;
				errors.Add( ( ProfileStatusCategory.Login, mismatchMsg ) );
			}
			else
			{
				LoginStatusText = "Checking server reachability…";
				if( !await OdooConnection.CorrectOdooAddress().WaitAsync( token ) )
					errors.Add( ( ProfileStatusCategory.Connection, "Invalid Odoo address or unreachable host." ) );
				else if( !await OdooConnection.CorrectOdooPort().WaitAsync( token ) )
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
		catch( OperationCanceledException ) when( token.IsCancellationRequested )
		{
			throw;
		}
		catch( Exception ex )
		{
			HackLogger.Error( "Login", $"Unexpected login error: {ex.Message}", ex );
			LogSession( StatusMessage.ERROR, ProfileStatusCategory.Login, $"Unexpected login error: {ex.Message}", updateBanner: true );
			return false;
		}
	}

	private Task StartInspectAsync( bool forceLog )
	{
		CancellationToken token = CtsHelper.Replace( ref _inspectCts ).Token;
		Task task = RunInspectAsync( forceLog, token );
		_inspectTask = task;
		return task;
	}

	private async Task WaitPendingInspectAsync( CancellationToken token )
	{
		if( _inspectTask is { IsCompleted: false } pending )
		{
			try
			{
				await pending.WaitAsync( token );
			}
			catch( OperationCanceledException ) when( !token.IsCancellationRequested )
			{
			}
		}
	}

	private async Task RunInspectAsync( bool forceLog, CancellationToken token )
	{
		try
		{
			EnvironmentSnapshot snapshot = await Task.Run( GatherEnvironmentSnapshot, token );
			if( token.IsCancellationRequested )
				return;

			ConnectionTargetText = snapshot.ConnectionTarget;
			List<(StatusMessage Status, string Category, string Message)> findings = snapshot.Findings;
			StringBuilder fp = new( findings.Count * 48 );
			(StatusMessage Status, string Category, string Message)? firstError = null;
			(StatusMessage Status, string Category, string Message)? firstWarning = null;
			for( int i = 0; i < findings.Count; i++ )
			{
				var f = findings[ i ];
				if( i > 0 )
					fp.Append( '|' );
				fp.Append( (int)f.Status ).Append( ':' ).Append( f.Category ).Append( ':' ).Append( f.Message );
				if( firstError is null && f.Status == StatusMessage.ERROR && f.Message.Length > 0 )
					firstError = f;
				else if( firstWarning is null && f.Status == StatusMessage.WARNING && f.Message.Length > 0 )
					firstWarning = f;
			}
			string fingerprint = fp.ToString();
			if( forceLog || !string.Equals( _session.LastReadinessFingerprint, fingerprint, StringComparison.Ordinal ) )
			{
				_session.LastReadinessFingerprint = fingerprint;
				_batchingSessionLogs = true;
				try
				{
					for( int i = 0; i < findings.Count; i++ )
					{
						var item = findings[ i ];
						_session.AddStatus( item.Status, item.Category, item.Message );
					}
				}
				finally
				{
					_batchingSessionLogs = false;
				}
				SyncFromSession();
			}

			if( firstError is { } err )
				SetBanner( StatusMessage.ERROR, err.Category, err.Message );
			else if( firstWarning is { } warn )
				SetBanner( StatusMessage.WARNING, warn.Category, warn.Message );
			else if( _session.LastErrors is { Count: > 0 } lastErrors )
			{
				BasicStatusMessage latest = lastErrors[ lastErrors.Count - 1 ];
				SetBanner( latest.Status, latest.Category, latest.Message ?? string.Empty );
			}
			else if( IsLoggedIn )
				SetBanner( StatusMessage.SUCCESS, ProfileStatusCategory.Login, $"Connected as {ConnectionTargetText}." );
		}
		catch( OperationCanceledException ) when( token.IsCancellationRequested )
		{
		}
		catch( Exception ex )
		{
			HackLogger.Warn( "ProfileManager", $"Environment inspection failed: {ex.Message}", ex );
		}
	}

	private EnvironmentSnapshot GatherEnvironmentSnapshot()
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

		if( _inspectSettings() is { } settingsItems )
			findings.AddRange( settingsItems );

		string connectionTarget = host.Length == 0
			? "No Odoo server configured yet"
			: $"{(user.Length > 0 ? user : "(no user)")} @ {(db.Length > 0 ? db : "(no db)")} — {url}";
		return new EnvironmentSnapshot( connectionTarget, findings );
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
		if( message.Contains( "must be set", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "database", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "address is not set", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "not a valid server", StringComparison.OrdinalIgnoreCase ) )
			return ProfileStatusCategory.Configuration;

		if( message.Contains( "unencrypted", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "timeout", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "timed out", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "refused", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "unreachable", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "ssl", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "tls", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "connect", StringComparison.OrdinalIgnoreCase ) )
			return ProfileStatusCategory.Connection;

		return ProfileStatusCategory.Login;
	}

	private void LogSession( StatusMessage status, string category, string message, bool updateBanner = false )
	{
		BasicStatusMessage entry = _session.AddStatus( status, category, message );
		if( updateBanner )
			UiDispatcher.RunOrPost( () => SetBanner( entry.Status, entry.Category, entry.Message ?? string.Empty ) );
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

	private void CountStatus( StatusMessage status )
	{
		switch( status )
		{
			case StatusMessage.ERROR: _errorCount++; break;
			case StatusMessage.WARNING: _warningCount++; break;
			default: _infoCount++; break;
		}
	}

	private void OnSessionMessageLogged( BasicStatusMessage entry )
	{
		if( _batchingSessionLogs )
			return;
		UiDispatcher.RunOrPost( () =>
		{
			OStatus.Add( entry );
			CountStatus( entry.Status );
			if( MatchesFilters( entry ) )
				FilteredStatus.Insert( 0, entry );
			if( entry.Status is StatusMessage.ERROR or StatusMessage.WARNING or StatusMessage.SUCCESS )
				SetBanner( entry.Status, entry.Category, entry.Message ?? string.Empty );
			UpdateCountsAndHints();
		} );
	}

	private void OnSessionMessagesCleared()
		=> UiDispatcher.RunOrPost( () =>
		{
			OStatus.ClearIfNonEmpty();
			FilteredStatus.ClearIfNonEmpty();
			SelectedStatusMessage = null;
			_errorCount = _warningCount = _infoCount = 0;
			UpdateCountsAndHints();
		} );

	private void SyncFromSession()
	{
		IReadOnlyList<BasicStatusMessage> lastErrors = _session.LastErrors;
		OStatus.ReplaceAll( lastErrors );
		_errorCount = _warningCount = _infoCount = 0;
		for( int i = 0; i < lastErrors.Count; i++ )
			CountStatus( lastErrors[ i ].Status );
		ApplyFilters();
	}

	private void ApplyFilters()
	{
		List<BasicStatusMessage> matching = new( OStatus.Count );
		for( int i = OStatus.Count - 1; i >= 0; i-- )
		{
			if( MatchesFilters( OStatus[ i ] ) )
				matching.Add( OStatus[ i ] );
		}
		FilteredStatus.ReplaceAll( matching );
		if( SelectedStatusMessage is { } current && !matching.Contains( current ) )
			SelectedStatusMessage = null;
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
		OnPropertyChanged( nameof( IsFilteredStatusEmpty ) );
		if( OStatus.Count == 0 )
		{
			StatusCountsText = "No status messages";
			EmptyHintTitle = "Ready to connect";
			EmptyHintSubtitle = "Click Odoo Login or Test Connection to record login, connection, config, and settings status.";
		}
		else
		{
			StringBuilder sb = new( 32 );
			if( _errorCount > 0 )
				sb.Append( _errorCount ).Append( _errorCount == 1 ? " error" : " errors" );
			if( _warningCount > 0 )
			{
				if( sb.Length > 0 ) sb.Append( " · " );
				sb.Append( _warningCount ).Append( _warningCount == 1 ? " warning" : " warnings" );
			}
			if( _infoCount > 0 )
			{
				if( sb.Length > 0 ) sb.Append( " · " );
				sb.Append( _infoCount ).Append( " info" );
			}
			StatusCountsText = sb.ToString();
			EmptyHintTitle = FilteredStatus.Count == 0 ? "No matching status messages" : "Ready to connect";
			EmptyHintSubtitle = FilteredStatus.Count == 0
				? "Change the area or severity filter above to view other recorded messages."
				: "Click Odoo Login below to authenticate and sync workspace metadata.";
		}
	}
}
