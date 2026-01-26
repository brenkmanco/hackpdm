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
using System.Collections;
using HackPDM.Core.General;

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
		GetInfoDefault();
    }

    private void SubmitOdooSettings(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        SetInfoDefault();

        this.Window?.Close();
    }
    private void GetInfoDefault()
    {
        txtOdooAddress.Text = OdooDefaults.Instance?.OdooAddress;
        txtOdooPort.Text = OdooDefaults.Instance?.OdooPort;
        changeURL();

        txtOdooDb.Text          = OdooDefaults.Instance?.OdooDb;
        txtSwKey.Text           = OdooDefaults.Instance?.OdooSwKey;
        txtAreaFactor.Text      = OdooDefaults.Instance?.OdooAreaFactor.ToString();
        txtOdooUser.Text        = OdooDefaults.Instance?.OdooUser ?? "";
        txtOdooPass.Password    = OdooDefaults.Instance?.OdooPass ?? "";
    }
    private async void SetInfoDefault()
    {
        OdooDefaults.Instance?.OdooAddress = txtOdooAddress.Text;
        OdooDefaults.Instance?.OdooPort = txtOdooPort.Text;

        StringBuilder sb = new();
        sb.Append($"http://{txtOdooAddress.Text}");
        if (txtOdooPort.Text is not null && txtOdooPort.Text.Length > 0)
        {
            sb.Append($":{txtOdooPort.Text}");
        }
        OdooDefaults.Instance?.OdooDb = txtOdooDb.Text;
        OdooDefaults.Instance?.OdooSwKey = txtSwKey.Text;
        decimal AF;
        if (!decimal.TryParse(txtAreaFactor.Text, out AF))
        {
            await MessageBox.ShowAsync("Area Factor must be a decimal number");
            return;
        }
        OdooDefaults.Instance?.OdooAreaFactor = AF;
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
