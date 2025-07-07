using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

using HackPDM.Properties;



namespace HackPDM.Forms.Hack
{
	public partial class HackSettings : Form
	{
		UserSettings userSettings;
		AppSettings appSettings;
		Assembly assembly;
		string documents;
		public HackSettings()
		{
			assembly		= Assembly.GetExecutingAssembly();
			documents		= Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			userSettings	= UserSettings.Default;
			appSettings		= AppSettings.Default;

			InitializeComponent();
			GetInfoDefaults();
		}
		private void GetInfoDefaults()
		{
			FileInfo hackExe = new FileInfo(assembly.Location);
			string assemblyDir = hackExe.DirectoryName;

			if (userSettings.PWAPathAbsolute is null or "")	txtPwaInput.Text = Path.Combine(documents, Application.ProductName, "pwa");
			else txtPwaInput.Text = userSettings.PWAPathAbsolute;
			
			if (userSettings.TemporaryPath is null or "") HackTempFolderPath.Text = Path.Combine(Path.GetTempPath(), Application.ProductName);
			else HackTempFolderPath.Text = userSettings.TemporaryPath;
		}

		private void btnSubmit_Click( object sender, EventArgs e )
		{
			StringBuilder errors = new();

			if ( !TryCreateDirectory( txtPwaInput.Text )) errors.AppendLine( "invalid pwa directory path" );
			if ( !TryCreateDirectory( HackTempFolderPath.Text )) errors.AppendLine( "invalid temporary directory path" );

			if (errors.Length > 0) 
			{
				errors.AppendLine("changes were not saved");
				MessageBox.Show(errors.ToString());
				return;
			}
			var dirInfo = new DirectoryInfo(txtPwaInput.Text);
			userSettings.PWAPathRelative = dirInfo.Name;
			userSettings.PWAPathAbsolute = txtPwaInput.Text;
			userSettings.TemporaryPath = HackTempFolderPath.Text;

			userSettings.Save();
			appSettings.Save();

			this.Close();
		}

		private bool TryCreateDirectory( string path )
		{
			if (Directory.Exists(path)) return true;

			try {Directory.CreateDirectory( path ); return true;}
			catch {return false;}
		}
	}
}
