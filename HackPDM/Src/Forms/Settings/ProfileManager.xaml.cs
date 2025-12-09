using System.Collections.Generic;
using System.Collections.ObjectModel;

using HackPDM.Data;
using HackPDM.Forms.Odoo;
using HackPDM.Hack;
using HackPDM.Helper;
using HackPDM.Odoo;
using HackPDM.Src.ClientUtils.Types;
using System.Runtime.InteropServices.WindowsRuntime;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CommunityToolkit.WinUI.UI.Controls;

using HackFileManager = HackPDM.Forms.Hack.HackFileManager;
using HackSettings = HackPDM.Properties.Settings;
using MessageBox = System.Windows.Forms.MessageBox;
using HackPDM.Src.Helper.Xaml;
using System.Threading.Tasks;
using Windows.UI.Composition;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM.Forms.Settings;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ProfileManager : Page, ISingletonPage<ProfileManager>
{
    public static ObservableCollection<BasicStatusMessage> OStatus { get; internal set; }           = [];
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
    private async Task<bool> AbleToLogin()
    {
        try
        {
            List<string> errors = [];
            

            if (!await OdooClient.CorrectOdooAddress())
            {
                errors.Add("invalid odoo address or unreachable host");
            }
            else if (!OdooClient.CorrectOdooPort())
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
			else if (!HackUpdater.EnsureUpdated(true))
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
    public void OdooSetting(object sender, RoutedEventArgs e)
    {
        //WindowConfig config = new("Odoo Settings", new Src.Data.Numeric.int4(0, 0, 1280, 720));
        WindowHelper.CreateWindowPage<OdooSettings>();
    }
    public void HackSetting(object sender, RoutedEventArgs e)
    {
        WindowHelper.CreateWindowPage<Hack.HackSettings>();
    }
    public async void AttemptLogin(object sender, RoutedEventArgs e)
    {
		OdooLoginProgressRing.IsActive = true;
		OdooLoginProgressRing.UpdateLayout();
		var IsLoggedIn = await AbleToLogin(); 
		OdooLoginProgressRing.IsActive = false;
		if (!IsLoggedIn) return;

        WindowHelper.CreateWindowPage<HackFileManager>();
    }
}