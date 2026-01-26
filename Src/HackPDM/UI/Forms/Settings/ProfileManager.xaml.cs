using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using CommunityToolkit.WinUI.UI.Controls;

using HackPDM.Configuration;
using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Core.Helper.Xaml;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.FormTransport;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;
using HackPDM.UI.Forms.FormTransport;
using HackPDM.UI.Forms.Hack;
using HackPDM.UI.Forms.Helper;
using HackPDM.UI.Forms.Odoo;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;

using Windows.UI.Composition;

using WinRT.Interop;

using static HackPDM.UI.Forms.Settings.ProfileManager;

using HackFileManager = HackPDM.UI.Forms.Hack.HackFileManager;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM.UI.Forms.Settings;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ProfileManager : Page
{
	public static bool IsLoggedIn { get; private set; }
	public static ObservableCollection<BasicStatusMessage> OStatus { get; internal set; } = [];
	private static readonly object LockObject = new();

	public ProfileManager()
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
		Home.NavigateToPage(NavigatePageMenu.Configuration);
		//OdooSettings oSettings = HackApp.Services?.GetRequiredService<OdooSettings>();
		//WindowHelper.CreateWindowFromPage<OdooSettings>( oSettings );
	}
	public async void HackSetting( object sender, RoutedEventArgs e )
	{
		Home.NavigateToPage(NavigatePageMenu.Configuration);
		//await MessageBox.ShowAsync( string.Join( " ", Enumerable.Range( 0, 200 ) ) );

		//HackSettings hSettings = HackApp.Services?.GetRequiredService<HackSettings>();
		//WindowHelper.CreateWindowFromPage<HackSettings>( hSettings );
	}
	private async Task<bool> AbleToLogin()
	{
		try
		{
			List<string> errors = [];


			if( !await OdooClient.CorrectOdooAddress() )
			{
				errors.Add( "invalid odoo address or unreachable host" );
			}
			else if( !await OdooClient.CorrectOdooPort() )
			{
				errors.Add( "invalid odoo port or server is down" );
			}
			else if( await OdooClient.CorrectUserId() is int status )
			{
				switch( status )
				{
					case 1:
						return true;
					case 0:
					{
						errors.Add( "invalid odoo credentials" );
						break;
					}
					default:
					{
						errors.Add( "odoo server isn't running" );
						break;
					}
				}
			}
			else if( !await HackUpdater.EnsureUpdated( true ) )
			{
				errors.Add( "running outdated client version" );
			}

			if( errors.Count > 0 )
			{
				foreach( string message in errors )
				{
					var listItem = GridHelp.EmptyListItem<BasicStatusMessage>(ProfileManStatusList);

					listItem.Status = StatusMessage.ERROR;
					listItem.Message = message;

					OStatus.Add( listItem );
				}
				return false;
			}
			return true;
		}
		catch
		{
			return false;
		}

	}
	private async void AttemptLogin( object sender, RoutedEventArgs e )
	{
		OdooLoginProgressRing.IsActive = true;
		OdooLoginProgressRing.UpdateLayout();
		IsLoggedIn = await AbleToLogin();
		OdooLoginProgressRing.IsActive = false;
		if( !IsLoggedIn )
			return;

		// now that login is verified, save credentials to windows credential manager
		( OdooDefaults.Instance as OdooDefaults )?.SaveCredentials();

		Home.NavigateToPage(NavigatePageMenu.HackFileManager);
	}
	private void LoadFromJson( string json )
	{

	}
#if DEBUG
	public static async void TestMessageBox()
	{
		string rawText = """
				// buttons on bottom / text box on top
				//		XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
				//		X																	X
				//		X																	X
				//		X																	X
				//		X							TEXTBOX									X
				//		X																	X
				//		X																	X
				//		X																	X
				//		XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
				//		X                 			Buttons									X
				//		XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
				// AHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH
				private void DefaultImpl()
				{
					ScrollContainer = new();
					TextBlockImpl = new()
					{
						Text = $"- {_message}",
						TextAlignment = TextAlignment.Justify,
						TextWrapping = TextWrapping.Wrap,
					};
					ScrollContainer.Content = TextBlockImpl;
					gridRoot.Children.Add( ScrollContainer );
					ScrollContainer.SetGrid(0, 0, 3, 3);
					var (primary, secondary, close) = DefaultButtons();
				}
				// buttons on bottom / list above
		//		
		//		XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
		""";

		DialogResult result = await MessageBox.ShowAsync(rawText, MessageBoxButtons.YesNoCancel);
	}
#endif

	public class SavedData
	{
		[JsonPropertyName( "OdooAddress" )] public string OdooAddress { get; set; }
		[JsonPropertyName( "OdooPort" )] public string OdooPort { get; set; }
		[JsonPropertyName( "OdooDb" )] public string OdooDb { get; set; }
		[JsonPropertyName( "PWAPathAbsolute" )] public string PWAPathAbsolute { get; set; }
		[JsonPropertyName( "OdooCredentialTarget" )] public string OdooCredentialTarget { get; set; }
		[JsonPropertyName( "Username" )] public string Username { get; set; }
		[JsonPropertyName( "Password" )] public string Password { get; set; }
	}

	private async void LoadJsonSettingsBtn_Click( object sender, RoutedEventArgs e )
	{
		// invalid cast?
		var hwnd = HackApp.Window?.IntPtrHandle ?? 0;
		var windowId = HackApp.Window?.AppWindow.Id ?? Win32Interop.GetWindowIdFromWindow(hwnd);
		var picker = new FileOpenPicker(windowId);
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
	private async Task SaveSettings( SavedData data )
	{
		var hwnd = HackApp.Window?.IntPtrHandle ?? 0;
		var windowId = HackApp.Window?.AppWindow.Id ?? Win32Interop.GetWindowIdFromWindow(hwnd);
		var picker = new FileSavePicker(windowId);
		picker.FileTypeChoices.Add( "Json file", [ ".json" ] );

		picker.SuggestedFolder = StorageBox.TemporaryPath;
		picker.SuggestedFileName = "hack-odoo";
		picker.DefaultFileExtension = ".json";

		var file = await picker.PickSaveFileAsync().AsTask();
		if( string.IsNullOrEmpty( file?.Path ) )
			return;

		await File.WriteAllTextAsync(
			file.Path,
			JsonSerializer.Serialize( data ) );
	}
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
	private async void CreateTemplateSettingsJson_Click( object sender, RoutedEventArgs e )
		=> await SaveSettings( _templateData );

}