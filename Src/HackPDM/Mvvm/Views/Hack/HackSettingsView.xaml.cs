using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;

using HackPDM.Abstractions;
using HackPDM.Core;
using HackPDM.Core.Configuration;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Mvvm.Views.Dialogs;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM.Mvvm.Views.Hack;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class HackSettingsView : Page
{
	Assembly assembly;
	string documents;

	public HackSettingsView()
	{
		assembly = Assembly.GetExecutingAssembly();
		documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

		InitializeComponent();
		GetInfoDefaults();
	}

	private void GetInfoDefaults()
	{
		FileInfo hackExe = new FileInfo(assembly.Location);
		string? assemblyDir = hackExe.DirectoryName;

		txtPwaInput.Text = HackDefaults.Instance?.PwaPathAbsolute is null or ""
			? Path.Combine(documents, StorageBox.APP_NAME, "pwa")
			: HackDefaults.Instance.PwaPathAbsolute;

		HackTempFolderPath.Text = StorageBox.TemporaryPath is null or "" 
			? Path.Combine(Path.GetTempPath(), StorageBox.APP_NAME) 
			: StorageBox.TemporaryPath;
	}
	private async void btnSubmit_Click(object sender, RoutedEventArgs e)
	{
		StringBuilder errors = new();

		if (!TryCreateDirectory( txtPwaInput.Text)) errors.AppendLine("invalid pwa directory path");
		if (!TryCreateDirectory( HackTempFolderPath.Text)) errors.AppendLine("invalid temporary directory path");

		if (errors.Length > 0)
		{
			errors.AppendLine("changes were not saved");
			await MessageBox.ShowAsync(errors.ToString());
			return;
		}
		var dirInfo = new DirectoryInfo(txtPwaInput.Text);
		HackDefaults.Instance?.PwaPathAbsolute = txtPwaInput.Text;
		HackDefaults.Instance?.PwaPathRelative = dirInfo.Name;
		StorageBox.TemporaryPath = HackTempFolderPath.Text;
		this.Window?.Close();
	}

	private static bool TryCreateDirectory(string path)
	{
		if (Directory.Exists(path)) return true;

		try { Directory.CreateDirectory(path); return true; }
		catch { return false; }
	}
}