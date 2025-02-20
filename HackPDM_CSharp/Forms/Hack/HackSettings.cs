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
		public HackSettings()
		{
			assembly = Assembly.GetExecutingAssembly();
			userSettings = UserSettings.Default;
			appSettings = AppSettings.Default;
			InitializeComponent();
			GetInfoDefaults();
		}
		private void GetInfoDefaults()
		{
			FileInfo hackExe = new FileInfo(assembly.Location);
			string assemblyDir = hackExe.DirectoryName;

			if (userSettings.PWAPathAbsolute is null or "")
			{
				txtPwaInput.Text	= Path.Combine(assemblyDir, "pwa");
			}
			else
			{
				txtPwaInput.Text        = userSettings.PWAPathAbsolute;
			}

			if (userSettings.ProjectDirectory is null or "")
			{
				txtProjectInput.Text	= assemblyDir;
			}
			else
			{
				txtProjectInput.Text    = userSettings.ProjectDirectory;
			}
			
			if (userSettings.TemporaryPath is null or "")
			{
				HackTempFolderPath.Text = Path.Combine(Path.GetTempPath(), assembly.GetName().Name);
			}
			else
			{
				HackTempFolderPath.Text = userSettings.TemporaryPath;
			}
			txtByteInput.Text       = appSettings.MeasureByteSize.ToString();
			txtFileInput.Text       = appSettings.MeasureFileSize;
		}

		private void btnSubmit_Click( object sender, EventArgs e )
		{
			StringBuilder errors = new();

			if ( !Directory.Exists( txtPwaInput.Text ) ) errors.AppendLine( "invalid pwa directory path" );
			if ( !Directory.Exists( txtProjectInput.Text ) ) errors.AppendLine( "invalid project directory path" );
			if ( !Directory.Exists( txtProjectInput.Text ) ) errors.AppendLine( "invalid project directory path" );
			if ( !double.TryParse( txtByteInput.Text, out double byteSize ) ) errors.AppendLine( "invalid byte size input" );

			if (errors.Length > 0) 
			{
				errors.AppendLine("changes were not saved");
				MessageBox.Show(errors.ToString());
				return;
			}
			var dirInfo = new DirectoryInfo(txtPwaInput.Text);
			userSettings.PWAPathRelative = dirInfo.Name;
			userSettings.PWAPathAbsolute = txtPwaInput.Text;
			userSettings.ProjectDirectory = txtProjectInput.Text;
			userSettings.TemporaryPath = HackTempFolderPath.Text;
			appSettings.MeasureByteSize = byteSize;
			appSettings.MeasureFileSize = txtFileInput.Text;
			appSettings.FileSizeMult = FileSizeMultiplier( txtFileInput.Text );

			userSettings.Save();
			appSettings.Save();

			this.Close();
		}
		private double FileSizeMultiplier( string fileSize )
		{
			switch ( fileSize )
			{
				case "KiloByte":
					return 1D;
				case "MegaByte":
					return 2D;
				case "GigaByte":
					return 3D;
				case "TeraByte":
					return 4D;
				case "Byte":
				default:
					return 0D;
			}
		}
	}
}
