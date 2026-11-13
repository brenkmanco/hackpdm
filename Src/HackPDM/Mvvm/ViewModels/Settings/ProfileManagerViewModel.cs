using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Configuration;
using HackPDM.Infrastructure.Operations;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.ViewModels.Common;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Mvvm.ViewModels.Settings;

/// <summary>
/// Logging in to Odoo, and loading or saving a login profile as JSON. One per visit of the Profile
/// Manager page; whether the session is logged in lives in <see cref="LoginSession"/>.
/// </summary>
/// <param name="session">The run-wide login state this page reads and updates.</param>
/// <param name="dialogs">Shows errors when a profile can't be read.</param>
/// <param name="pickers">Chooses profile files to load and save.</param>
/// <param name="navigation">Moves to the file manager after a login, or to the settings page.</param>
/// <param name="accountChanged">Called after a login as another server, database or user than the last one.</param>
public partial class ProfileManagerViewModel(
	LoginSession session,
	IDialogService dialogs,
	IFilePickerService pickers,
	INavigationService navigation,
	Action? accountChanged = null ) : ObservableObject
{
	private const string ProfileExtension = ".json";

	/// <summary>Why the last login failed.</summary>
	public ObservableCollection<BasicStatusMessage> OStatus { get; } = new( session.LastErrors );

	/// <summary>Whether this session is logged in; stored in the run-wide <see cref="LoginSession"/>.</summary>
	public bool IsLoggedIn
	{
		get => session.IsLoggedIn;
		set
		{
			if( session.IsLoggedIn == value )
				return;
			session.IsLoggedIn = value;
			OnPropertyChanged();
		}
	}

	[ObservableProperty] public partial bool IsLoggingIn { get; set; }

	/// <summary>Logs in; on success saves the credentials and opens the file manager.</summary>
	[RelayCommand]
	private async Task LoginAsync()
	{
		IsLoggingIn = true;
		try
		{
			IsLoggedIn = await AbleToLoginAsync();
		}
		finally
		{
			IsLoggingIn = false;
		}
		if( !IsLoggedIn )
			return;
		var odoo = OdooDefaults.Instance!;
		if( session.LoggedInAs( $"{odoo.OdooUrl}|{odoo.OdooDb}|{odoo.OdooUser}" ) )
			accountChanged?.Invoke();
		// the login is verified, so keep the credentials in the system keychain (see CrossPlatformCredentialManager)
		( OdooDefaults.Instance as OdooDefaults )?.SaveCredentials();
		navigation.NavigateTo( NavigatePageMenu.HackFileManager );
	}

	/// <summary>Opens the configuration page.</summary>
	[RelayCommand]
	private void OpenSettings() => navigation.NavigateTo( NavigatePageMenu.Configuration );

	/// <summary>Lets the user pick a profile file and applies its values to the live settings (without logging in).</summary>
	[RelayCommand]
	private async Task LoadProfileAsync()
	{
		if( await pickers.PickOpenFileAsync( ProfileExtension ) is not { } path )
			return;
		SavedProfileData? profile;
		try
		{
			profile = await ProfileStore.ReadAsync( path );
		}
		catch( JsonException )
		{
			profile = null;
		}
		if( profile is null )
		{
			await dialogs.ShowMessageAsync( "unable to deserialize json file" );
			return;
		}
		ProfileStore.Apply( profile );
	}

	/// <summary>Saves the current settings, including the password, to a profile file the user picks.</summary>
	[RelayCommand]
	private Task SaveProfileAsync() => SaveAsync( ProfileStore.Current( includePassword: true ) );

	/// <summary>Saves the current settings, without the password, to a profile file the user picks.</summary>
	[RelayCommand]
	private Task SaveProfileWithoutPasswordAsync() => SaveAsync( ProfileStore.Current( includePassword: false ) );

	/// <summary>Saves an example profile with placeholder values to a file the user picks.</summary>
	[RelayCommand]
	private Task CreateTemplateAsync() => SaveAsync( ProfileStore.Template );

	private async Task SaveAsync( SavedProfileData profile )
	{
		if( await pickers.PickSaveFileAsync( "Json file", ProfileExtension, "hack-odoo" ) is { } path )
			await ProfileStore.WriteAsync( path, profile );
	}

	/// <summary>Checks the server address, port and credentials; the reasons it can't log in go to <see cref="OStatus"/>.</summary>
	/// <returns><see langword="true"/> when the credentials were accepted.</returns>
	/// <remarks>
	/// Re-initializes <see cref="HackPDM.Infrastructure.Odoo.OdooDefaults.Instance"/> (which authenticates against the server),
	/// then checks the address and the port. The errors are also kept in the <see cref="LoginSession"/> so they show again when
	/// the page is reopened. Any exception is swallowed and reported as <see langword="false"/> with no message. Once logged
	/// in, a client older than the server's <c>client_version</c> is refused and offered the update
	/// (<see cref="HackPDM.Configuration.HackUpdater.EnsureUpdated"/>).
	/// </remarks>
	public async Task<bool> AbleToLoginAsync()
	{
		OStatus.Clear();
		session.LastErrors = [];
		try
		{
			List<string> errors = [];

			await OdooDefaults.Instance!.InitializeAsync();
			if( !await OdooConnection.CorrectOdooAddress() )
				errors.Add( "invalid odoo address or unreachable host" );
			else if( !await OdooConnection.CorrectOdooPort() )
				errors.Add( "invalid odoo port or server is down" );
			else if( OdooConnection.CorrectUserId() is int status )
			{
				switch( status )
				{
					case 1:
						// logged in: the server may still require a newer client
						if( await HackUpdater.EnsureUpdated( true ) )
							return true;
						errors.Add( $"this client ({ClientUpdate.CurrentText}) is older than the server requires; install the update" );
						break;
					case 0:
						errors.Add( "invalid odoo credentials" );
						break;
					default:
						errors.Add( "odoo server isn't running" );
						break;
				}
			}

			foreach( string message in errors )
				OStatus.Add( new BasicStatusMessage { Status = StatusMessage.ERROR, Message = message } );
			session.LastErrors = [ .. OStatus ];
			return errors.Count == 0;
		}
		catch
		{
			return false;
		}
	}
}
