using CredentialManagement;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HackPDM
{
    public partial class OdooSettings : Form
    {
        public OdooSettings()
        {
            InitializeComponent();
            GetInfoDefault();
        }

        private void btnSubmit_Click(object sender, EventArgs e)
        {
            SetInfoDefault();
        }
        private void GetInfoDefault()
        {
            txtOdooAddress.Text = Properties.UserSettings.Default.OdooAddress;
            txtOdooPort.Text = Properties.UserSettings.Default.OdooPort;
            changeURL();

            txtOdooDb.Text = Properties.UserSettings.Default.OdooDb;
            txtSwKey.Text = Properties.AppSettings.Default.SwLicenseKey;
            txtAreaFactor.Text = Properties.AppSettings.Default.AreaFactor.ToString();
            var cm = new Credential { Target = Properties.AppSettings.Default.OdooCredentialTarget };
            if (cm.Load())
            {
                txtOdooUser.Text = cm.Username;
                txtOdooPass.Text = cm.Password;
            }
        }
        private void SetInfoDefault()
        {
            Properties.UserSettings.Default.OdooAddress = txtOdooAddress.Text;
            Properties.UserSettings.Default.OdooPort = txtOdooPort.Text;

            StringBuilder sb = new();
            sb.Append($"http://{txtOdooAddress.Text}");
            if (txtOdooPort.Text is not null && txtOdooPort.Text.Length > 0)
            {
                sb.Append($":{txtOdooPort.Text}");
            }
            Properties.UserSettings.Default.OdooDb = txtOdooDb.Text;
            Properties.AppSettings.Default.SwLicenseKey = txtSwKey.Text;
            Decimal AF;
            if (!Decimal.TryParse(txtAreaFactor.Text, out AF))
            {
                MessageBox.Show("Area Factor must be a decimal number");
                return;
            }
            Properties.AppSettings.Default.AreaFactor = AF;
            Properties.UserSettings.Default.Save();
            Properties.AppSettings.Default.Save();

            Credential cm = new() { Target = Properties.AppSettings.Default.OdooCredentialTarget };
            if (cm.Load())
                cm.Delete();

            Credential cred = new()
            {
                Target = HackPDM.Properties.AppSettings.Default.OdooCredentialTarget,
                Username = txtOdooUser.Text,
                Password = txtOdooPass.Text,
                PersistanceType = PersistanceType.LocalComputer
            };
            cred.Save();

            OdooDefaults.OdooUser = txtOdooUser.Text;
            OdooDefaults.OdooPass = txtOdooPass.Text;
            OdooDefaults.OdooUrl = sb.ToString();
            OdooDefaults.OdooID = 0;

            Close();
        }

		private void textBox1_TextChanged( object sender, EventArgs e ) => changeURL();

		private void txtOdooUrl_TextChanged( object sender, EventArgs e ) => changeURL();
        private void changeURL()
        {
			StringBuilder sb = new();
			sb.Append( $"Odoo Url: \thttp://" );
			if ( txtOdooAddress.Text is not null and not "" )
			{
				sb.Append( $"{txtOdooAddress.Text}" );
			}
			else
			{
				sb.Append( "<address>" );
			}
			if ( txtOdooPort.Text is not null and not "" )
			{
				sb.Append( $":{txtOdooPort.Text}" );
			}
			label2.Text = sb.ToString();
		}
	}
}
