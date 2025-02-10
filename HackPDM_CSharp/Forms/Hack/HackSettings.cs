using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

using HackPDM.Properties;



namespace HackPDM.Forms.Hack
{
	public partial class HackSettings : Form
	{
		UserSettings userSettings;
		AppSettings appSettings;
		public HackSettings()
		{
			userSettings = UserSettings.Default;
			appSettings = AppSettings.Default;
			InitializeComponent();
			GetInfoDefaults();
		}
		private void GetInfoDefaults()
		{
			txtPwaInput.Text        = userSettings.PWAPathAbsolute;
			txtProjectInput.Text    = userSettings.ProjectDirectory;
			txtByteInput.Text       = appSettings.MeasureByteSize.ToString();
			txtFileInput.Text       = appSettings.MeasureFileSize;
		}

		private void btnSubmit_Click( object sender, EventArgs e )
		{
			List<string> errors = new List<string>();

			if ( !Directory.Exists( txtPwaInput.Text ) ) errors.Add( "invalid pwa directory path" );
			else
			{
				var dirInfo = new DirectoryInfo(txtPwaInput.Text);
				userSettings.PWAPathRelative = dirInfo.Name;
			}
			
			if ( !Directory.Exists( txtProjectInput.Text ) )
				errors.Add( "invalid project directory path" );
			if ( !double.TryParse( txtByteInput.Text, out double byteSize ) )
				errors.Add( "invalid byte size input" );


			userSettings.PWAPathAbsolute = txtPwaInput.Text;
			userSettings.ProjectDirectory = txtProjectInput.Text;
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
