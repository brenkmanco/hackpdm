using System;
using System.Text;
using System.Windows;

using HackPDM.Odoo;

using Meziantou.Framework.Win32;
using Sett = HackPDM.Properties.Settings;
using Window = Microsoft.UI.Xaml.Window;

using Microsoft.UI.Xaml.Controls;
using HackPDM.Helper;
using HackPDM.Extensions.Controls;
using HackPDM.Src.ClientUtils.Types;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM.Forms.Odoo;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class OdooSettings : Page, ISingletonPage<OdooSettings>
{
	public static OdooSettings? Singleton { get; }
    public OdooSettings()
    {
        InitializeComponent();
        GetInfoDefault();
    }

    private void SubmitOdooSettings(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        SetInfoDefault();

        this.Window?.Close();
    }
    private void GetInfoDefault()
    {
        txtOdooAddress.Text = Sett.Get<string>("OdooAddress");
        txtOdooPort.Text = Sett.Get<string>("OdooPort");
        changeURL();

        txtOdooDb.Text = Sett.Get<string>("OdooDb");
        txtSwKey.Text = Sett.Get<string>("SwLicenseKey");
        txtAreaFactor.Text = Sett.Get<decimal>("AreaFactor").ToString();
        Credential? cred = CredentialManager.ReadCredential("HackPDM-OdooUser");

        txtOdooUser.Text = cred?.UserName ?? "";
        txtOdooPass.Password = cred?.Password ?? "";
    }
    private void SetInfoDefault()
    {
        const string credTarget = "HackPDM-OdooUser";
        Sett.Set("OdooAddress", txtOdooAddress.Text);
        Sett.Set("OdooPort", txtOdooPort.Text);

        StringBuilder sb = new();
        sb.Append($"http://{txtOdooAddress.Text}");
        if (txtOdooPort.Text is not null && txtOdooPort.Text.Length > 0)
        {
            sb.Append($":{txtOdooPort.Text}");
        }
        Sett.Set("OdooDb", txtOdooDb.Text);
        Sett.Set("SwLicenseKey", txtSwKey.Text);
        decimal AF;
        if (!decimal.TryParse(txtAreaFactor.Text, out AF))
        {
            MessageBox.Show("Area Factor must be a decimal number");
            return;
        }
        Sett.Set("AreaFactor", AF);

        Credential? cred = CredentialManager.ReadCredential(credTarget) 
            ?? new(CredentialType.Generic,
                    Sett.Get<string>(credTarget) ?? credTarget,
                    txtOdooUser.Text,
                    txtOdooPass.Password,
                    "HackPDM Odoo Credentials");
        
        
        OdooDefaults.OdooPass = txtOdooPass.Password;
        OdooDefaults.OdooUser = txtOdooUser.Text;
        OdooDefaults.OdooUrl = sb.ToString();
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
