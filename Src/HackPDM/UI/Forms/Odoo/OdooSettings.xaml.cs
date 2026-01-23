using System;
using System.Text;
using HackPDM.Abstractions;
using Meziantou.Framework.Win32;
using Window = Microsoft.UI.Xaml.Window;

using Microsoft.UI.Xaml.Controls;
using HackPDM.Infrastructure.Odoo;
using HackPDM.UI.Controls;
using HackPDM.UI.Compatibility;
using HackPDM.Core.Configuration;
using Microsoft.UI.Xaml.Navigation;
using HackPDM.UI.Forms.Helper;
using HackPDM.Shared.GlobalData;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM.UI.Forms.Odoo;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class OdooSettings : Page
{
    
    private static CoreSettings? Sett;
    readonly string credTarget = StorageBox.DEFAULT_ODOO_CREDENTIALS;
    public OdooSettings()
    {
        InitializeComponent();
    }

    public OdooSettings(ISettingsProvider settingsProvider) : this()
    {
        Sett = settingsProvider as CoreSettings;
		GetInfoDefault();
	}
    private void SubmitOdooSettings(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        SetInfoDefault();

        this.Window?.Close();
    }
    private void GetInfoDefault()
    {
        txtOdooAddress.Text = Sett?.Get<string>("OdooAddress");
        txtOdooPort.Text = Sett?.Get<string>("OdooPort");
        changeURL();

        txtOdooDb.Text = Sett?.Get<string>("OdooDb");
        txtSwKey.Text = Sett?.Get<string>("SwLicenseKey");
        txtAreaFactor.Text = Sett?.Get<decimal>("AreaFactor").ToString();
        
        txtOdooUser.Text = OdooDefaults.Instance?.OdooUser ?? "";
        txtOdooPass.Password = OdooDefaults.Instance?.OdooPass ?? "";
    }
    private async void SetInfoDefault()
    {
        Sett?.Set("OdooAddress", txtOdooAddress.Text);
        Sett?.Set("OdooPort", txtOdooPort.Text);

        StringBuilder sb = new();
        sb.Append($"http://{txtOdooAddress.Text}");
        if (txtOdooPort.Text is not null && txtOdooPort.Text.Length > 0)
        {
            sb.Append($":{txtOdooPort.Text}");
        }
        Sett?.Set("OdooDb", txtOdooDb.Text);
        Sett?.Set("SwLicenseKey", txtSwKey.Text);
        decimal AF;
        if (!decimal.TryParse(txtAreaFactor.Text, out AF))
        {
            await MessageBox.ShowAsync("Area Factor must be a decimal number");
            return;
        }
        Sett?.Set("AreaFactor", AF);
        string cTarget = OdooDefaults.Instance?.OdooCredentialTarget ?? credTarget;
        OdooDefaults.Instance?.OdooUser = txtOdooUser.Text;
        OdooDefaults.Instance?.OdooPass = txtOdooPass.Password;     
        
        OdooDefaults.Instance?.OdooUrl = sb.ToString();
    }

    private void textBox1_TextChanged(object sender, EventArgs e) => changeURL();
    private void txtOdooUrl_TextChanged(object sender, EventArgs e) => changeURL();
    private void changeURL()
    {
        StringBuilder sb = new();
        sb.Append($"Odoo Url: \thttp://");
        if (txtOdooAddress.Text is not null and not "")
        {
            sb.Append($"{txtOdooAddress.Text}");
        }
        else
        {
            sb.Append("<address>");
        }
        if (txtOdooPort.Text is not null and not "")
        {
            sb.Append($":{txtOdooPort.Text}");
        }
        // label2.Text = sb.ToString();
    }
}
