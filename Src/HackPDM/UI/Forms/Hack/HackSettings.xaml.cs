using System;
using System.IO;
using System.Reflection;
using System.Text;
using HackPDM.Core;
using HackPDM.Core.Configuration;
using HackPDM.Shared.GlobalData;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using HackPDM.UI.Controls;
using HackPDM.Abstractions;
using HackPDM.Core.Hack;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM.UI.Forms.Hack;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class HackSettings : Page
{
	Assembly assembly;
	ISettingsProvider settingsProvider;
	string documents;

	public HackSettings()
	{
		assembly = Assembly.GetExecutingAssembly();
		documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

		InitializeComponent();
		GetInfoDefaults();
	}
	public HackSettings(ISettingsProvider settingsProvider) : this() 
	{
		this.settingsProvider = settingsProvider;
	}
	private void GetInfoDefaults()
	{
		FileInfo hackExe = new FileInfo(assembly.Location);
		string? assemblyDir = hackExe.DirectoryName;

		if (HackDefaults.Instance.PwaPathAbsolute is null or "") txtPwaInput.Text = Path.Combine(documents, StorageBox.APP_NAME, "pwa");
		else txtPwaInput.Text = HackDefaults.Instance.PwaPathAbsolute;

		if (StorageBox.TemporaryPath is null or "") HackTempFolderPath.Text = Path.Combine(Path.GetTempPath(), StorageBox.APP_NAME);
		else HackTempFolderPath.Text = StorageBox.TemporaryPath;
	}
	private void btnSubmit_Click(object sender, RoutedEventArgs e)
	{
		StringBuilder errors = new();

		if (!TryCreateDirectory(txtPwaInput.Text)) errors.AppendLine("invalid pwa directory path");
		if (!TryCreateDirectory(HackTempFolderPath.Text)) errors.AppendLine("invalid temporary directory path");

		if (errors.Length > 0)
		{
			errors.AppendLine("changes were not saved");
			MessageBox.Show(errors.ToString());
			return;
		}
		var dirInfo = new DirectoryInfo(txtPwaInput.Text);
		HackDefaults.Instance.PwaPathAbsolute = txtPwaInput.Text;
		HackDefaults.Instance.PwaPathRelative = dirInfo.Name;
		StorageBox.TemporaryPath = HackTempFolderPath.Text;
		this.Window?.Close();
	}

	private bool TryCreateDirectory(string path)
	{
		if (Directory.Exists(path)) return true;

		try { Directory.CreateDirectory(path); return true; }
		catch { return false; }
	}
}