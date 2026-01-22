using System.Collections.Generic;
using System.Collections.ObjectModel;


using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CommunityToolkit.WinUI.UI.Controls;

using HackFileManager = HackPDM.UI.Forms.Hack.HackFileManager;
using System.Threading.Tasks;
using Windows.UI.Composition;
using HackPDM.Configuration;
using HackPDM.Core;
using HackPDM.Core.Helper.Xaml;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.FormTransport;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;
using HackPDM.UI.Forms.Odoo;
using Microsoft.Extensions.DependencyInjection;
using HackPDM.UI.Forms.Hack;
using HackPDM.UI.Forms.Helper;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.UI.Forms.FormTransport;
using System.Linq;
using System;
using System.Text.Json.Serialization;
using Microsoft.Windows.Storage.Pickers;
using WinRT.Interop;
using Microsoft.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM.UI.Forms.Settings;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ProfileManager : Page
{
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

	public void OdooSetting(object sender, RoutedEventArgs e)
	{
		OdooSettings oSettings = HackApp.Services?.GetRequiredService<OdooSettings>();
		WindowHelper.CreateWindowFromPage<OdooSettings>(oSettings);
	}
	public async void HackSetting(object sender, RoutedEventArgs e)
	{
		MessageBox.ShowAsync(string.Join(" ", Enumerable.Range(0, 1000)));

		HackSettings hSettings = HackApp.Services?.GetRequiredService<HackSettings>();
		WindowHelper.CreateWindowFromPage<HackSettings>(hSettings);
	}
	private async Task<bool> AbleToLogin()
	{
		try
		{
			List<string> errors = [];


			if (!await OdooClient.CorrectOdooAddress())
			{
				errors.Add("invalid odoo address or unreachable host");
			}
			else if (!await OdooClient.CorrectOdooPort())
			{
				errors.Add("invalid odoo port or server is down");
			}
			else if (await OdooClient.CorrectUserId() is int status)
			{
				switch (status)
				{
					case 1: return true;
					case 0:
						{
							errors.Add("invalid odoo credentials");
							break;
						}
					default:
						{
							errors.Add("odoo server isn't running");
							break;
						}
				}
			}
			else if (!await HackUpdater.EnsureUpdated(true))
			{
				errors.Add("running outdated client version");
			}

			if (errors.Count > 0)
			{
				foreach (string message in errors)
				{
					var listItem = GridHelp.EmptyListItem<BasicStatusMessage>(ProfileManStatusList);

					listItem.Status = StatusMessage.ERROR;
					listItem.Message = message;

					OStatus.Add(listItem);
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
	private async void AttemptLogin(object sender, RoutedEventArgs e)
	{
		OdooLoginProgressRing.IsActive = true;
		OdooLoginProgressRing.UpdateLayout();
		var IsLoggedIn = await AbleToLogin();
		OdooLoginProgressRing.IsActive = false;
		if (!IsLoggedIn) return;

		Window window = WindowHelper.CreateWindow<Window>("HackFileManager");
		HackFileManager hack = HackApp.Services.GetRequiredService<HackFileManager>();
		hack.LoadHackMan();
		(window.Content as Frame)?.Content = hack;
	}
	private void LoadFromJson(string json)
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
		[JsonPropertyName("OdooAddress")] public string OdooAddress { get; set; }
		[JsonPropertyName("OdooPort")] public string OdooPort { get; set; }
		[JsonPropertyName("OdooDb")] public string OdooDb { get; set; }
		[JsonPropertyName("PWAPathAbsolute")] public string PWAPathAbsolute { get; set; }
		[JsonPropertyName("OdooCredentialTarget")] public string OdooCredentialTarget { get; set; }
		[JsonPropertyName("UserName")] public string UserName { get; set; }
	}

	private async void LoadJsonSettingsBtn_Click(object sender, RoutedEventArgs e)
	{

		// invalid cast?

		var hwnd = WindowNative.GetWindowHandle(HackApp.Window);
		var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
		var picker = new FileOpenPicker(windowId);
		picker.FileTypeFilter.Add(".json");
		var file = await picker.PickSingleFileAsync();
	}
}