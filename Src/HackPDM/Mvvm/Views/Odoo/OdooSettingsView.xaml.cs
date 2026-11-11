using System;
using System.Text;
using HackPDM.Abstractions;
using Meziantou.Framework.Win32;
using Window = Microsoft.UI.Xaml.Window;

using Microsoft.UI.Xaml.Controls;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Core.Configuration;
using Microsoft.UI.Xaml.Navigation;
using HackPDM.Mvvm.Views.Dialogs;
using HackPDM.Shared.GlobalData;
using System.Collections;
using HackPDM.Core.General;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM.Mvvm.Views.Odoo;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class OdooSettingsView : Page
{
    readonly string credTarget = StorageBox.DEFAULT_ODOO_CREDENTIALS;
    public OdooSettingsView()
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

}
