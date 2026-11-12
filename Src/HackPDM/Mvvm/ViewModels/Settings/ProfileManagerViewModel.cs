using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Configuration;
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

	[RelayCommand]
	private void OpenSettings() => navigation.NavigateTo( NavigatePageMenu.Configuration );

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

	[RelayCommand]
	private Task SaveProfileAsync() => SaveAsync( ProfileStore.Current( includePassword: true ) );

	[RelayCommand]
	private Task SaveProfileWithoutPasswordAsync() => SaveAsync( ProfileStore.Current( includePassword: false ) );

	[RelayCommand]
	private Task CreateTemplateAsync() => SaveAsync( ProfileStore.Template );

	private async Task SaveAsync( SavedProfileData profile )
	{
		if( await pickers.PickSaveFileAsync( "Json file", ProfileExtension, "hack-odoo" ) is { } path )
			await ProfileStore.WriteAsync( path, profile );
	}

	/// <summary>Checks the server, port, credentials and client version; the reasons it can't log in go to <see cref="OStatus"/>.</summary>
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
						return true;
					case 0:
						errors.Add( "invalid odoo credentials" );
						break;
					default:
						errors.Add( "odoo server isn't running" );
						break;
				}
			}
			else if( !await HackUpdater.EnsureUpdated( true ) )
				errors.Add( "running outdated client version" );

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
