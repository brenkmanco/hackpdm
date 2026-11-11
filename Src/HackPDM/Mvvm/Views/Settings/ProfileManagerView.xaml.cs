using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


using HackPDM.Configuration;
using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.Views.Dialogs;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

using Windows.UI.Composition;

#if WINDOWS
using WinRT.Interop;
#endif

using static HackPDM.Mvvm.Views.Settings.ProfileManagerView;
using HackPDM.Shell;
using HackPDM.Mvvm.Views.Hack;

using HackFileManager = HackPDM.Mvvm.Views.Hack.HackFileManagerView;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM.Mvvm.Views.Settings;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ProfileManagerView : Page
{
	public PM_VM ViewModel => InstanceManager.PM;
	public static bool IsLoggedIn 
	{ 
		get => InstanceManager.PM.IsLoggedIn; 
		set => InstanceManager.PM.IsLoggedIn = value; 
	}
	public static ObservableCollection<BasicStatusMessage> OStatus => InstanceManager.PM.OStatus;
	private readonly SavedData _templateData = new()
	{
		OdooAddress = "10.0.0.68",
		OdooCredentialTarget = StorageBox.DEFAULT_ODOO_CREDENTIALS,
		OdooDb = "odoopdm",
		OdooPort = "8069",
		PWAPathAbsolute = @"C:\path\to\pwa",
		Username = "{username}",
		Password = "{password} -leave password blank if you dont want to save password to file. " +
			"Windows Credential Manager will pick it up with OdooCredentialTarget and Odoo Settings Window" +
			"Allows for saving to Windows Credential Manager directly"
	};

	public ProfileManagerView()
	{
		InitializeComponent();
		LoadSettings();
	}
	private void LoadSettings()
	{
		HackApp.Window?.Title = "Profile Manager - HackPDM";

		ProfileManStatusList.ItemsSource = OStatus;

		odooSettingsBtn.Click += OdooSetting;
		HackSettingsBtn.Click += HackSetting;
		OdooLoginBtn.Click += AttemptLogin;
	}

	public void OdooSetting( object sender, RoutedEventArgs e )
	{
		HomeView.NavigateToPage(NavigatePageMenu.Configuration);
		//OdooSettings oSettings = HackApp.Services?.GetRequiredService<OdooSettings>();
		//WindowHelper.CreateWindowFromPage<OdooSettings>( oSettings );
	}
	public async void HackSetting( object sender, RoutedEventArgs e )
	{
		HomeView.NavigateToPage(NavigatePageMenu.Configuration);
		//await MessageBox.ShowAsync( string.Join( " ", Enumerable.Range( 0, 200 ) ) );

		//HackSettings hSettings = HackApp.Services?.GetRequiredService<HackSettings>();
		//WindowHelper.CreateWindowFromPage<HackSettings>( hSettings );
	}
	private async void AttemptLogin( object sender, RoutedEventArgs e )
	{
		OdooLoginProgressRing.IsActive = true;
		OdooLoginProgressRing.UpdateLayout();
		IsLoggedIn = await ViewModel.AbleToLoginAsync();
		OdooLoginProgressRing.IsActive = false;
		if( !IsLoggedIn )
			return;

		// now that login is verified, save credentials to windows credential manager
		( OdooDefaults.Instance as OdooDefaults )?.SaveCredentials();

		HomeView.NavigateToPage(NavigatePageMenu.HackFileManager);
	}

	public class SavedData : SavedProfileData { }

	private async void LoadJsonSettingsBtn_Click( object sender, RoutedEventArgs e )
	{



		var picker = new FileOpenPicker();
#if WINDOWS
		if (HackApp.Window != null)
		{
			WinRT.Interop.InitializeWithWindow.Initialize(picker, HackApp.Window.IntPtrHandle);
		}
#endif
		picker.FileTypeFilter.Add( ".json" );
		picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

		var file = await picker.PickSingleFileAsync().AsTask();



		if( string.IsNullOrEmpty( file?.Path ) )
			return;

		var savedData = JsonSerializer.Deserialize<SavedData>(await File.ReadAllTextAsync( file.Path, Encoding.UTF8 ));

		if( savedData is null )
		{
			await MessageBox.ShowAsync( "unable to deserialize json file" );
			return;
		}

		if( !string.IsNullOrEmpty( savedData.OdooAddress ) )
			OdooDefaults.Instance?.OdooAddress = savedData.OdooAddress;
		if( !string.IsNullOrEmpty( savedData.OdooPort ) )
			OdooDefaults.Instance?.OdooPort = savedData.OdooPort;
		if( !string.IsNullOrEmpty( savedData.OdooDb ) )
			OdooDefaults.Instance?.OdooDb = savedData.OdooDb;
		if( !string.IsNullOrEmpty( savedData.OdooCredentialTarget ) )
			OdooDefaults.Instance?.OdooCredentialTarget = savedData.OdooCredentialTarget;
		if( !string.IsNullOrEmpty( savedData.PWAPathAbsolute ) )
			HackDefaults.Instance?.PwaPathAbsolute = savedData.PWAPathAbsolute;

		string? username = !string.IsNullOrEmpty( savedData.Username )
			? savedData.Username
			: !string.IsNullOrEmpty(OdooDefaults.Instance?.OdooUser)
				? OdooDefaults.Instance?.OdooUser
				: null;

		string? password = !string.IsNullOrEmpty( savedData.Password )
			? savedData.Password
			: !string.IsNullOrEmpty(OdooDefaults.Instance?.OdooPass)
				? OdooDefaults.Instance?.OdooPass
				: null;

		if( username is null || password is null )
			return;

		OdooDefaults.Instance?.OdooUser = username;
		OdooDefaults.Instance?.OdooPass = password;
	}
	private async void SaveSettingsJson_Click( object sender, RoutedEventArgs e )
	{
		var saveData = new SavedData()
		{
			OdooAddress             = OdooDefaults.Instance?.OdooAddress ?? "",
			OdooPort                = OdooDefaults.Instance?.OdooPort ?? "",
			OdooDb                  = OdooDefaults.Instance?.OdooDb ?? "",
			OdooCredentialTarget    = OdooDefaults.Instance?.OdooCredentialTarget ?? "",
			PWAPathAbsolute         = HackDefaults.Instance?.PwaPathAbsolute ?? "",
			Username                = OdooDefaults.Instance?.OdooUser ?? "",
			Password                = OdooDefaults.Instance?.OdooPass ?? "",
		};
		await SaveSettings( saveData );
	}
	private async void SaveSettingsJsonWithoutPassword_Click( object sender, RoutedEventArgs e )
	{
		var saveData = new SavedData()
		{
			OdooAddress             = OdooDefaults.Instance?.OdooAddress ?? "",
			OdooPort                = OdooDefaults.Instance?.OdooPort ?? "",
			OdooDb                  = OdooDefaults.Instance?.OdooDb ?? "",
			OdooCredentialTarget    = OdooDefaults.Instance?.OdooCredentialTarget ?? "",
			PWAPathAbsolute         = HackDefaults.Instance?.PwaPathAbsolute ?? "",
			Username                = OdooDefaults.Instance?.OdooUser ?? "",
		};
		await SaveSettings( saveData );
	}
	private static async Task SaveSettings( SavedData data )
	{


		var picker = new FileSavePicker();
#if WINDOWS
		if (HackApp.Window != null)
		{
			WinRT.Interop.InitializeWithWindow.Initialize(picker, HackApp.Window.IntPtrHandle);
		}
#endif
		picker.FileTypeChoices.Add( "Json file", [ ".json" ] );

		picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
		picker.SuggestedFileName = "hack-odoo";
		picker.DefaultFileExtension = ".json";

		var file = await picker.PickSaveFileAsync().AsTask();
		if( string.IsNullOrEmpty( file?.Path ) )
			return;

		await File.WriteAllTextAsync(
			file.Path,
			JsonSerializer.Serialize( data ) );
	}
	private async void CreateTemplateSettingsJson_Click( object sender, RoutedEventArgs e )
		=> await SaveSettings( _templateData );

}