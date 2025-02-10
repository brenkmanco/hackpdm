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
            SetInfoDefault(txtOdooUrl.Text, txtOdooDb.Text, txtOdooUser.Text, txtOdooPass.Text, txtSwKey.Text, txtAreaFactor.Text);
        }
        private void GetInfoDefault()
        {
            txtOdooUrl.Text = Properties.UserSettings.Default.OdooUrl;
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
        private void SetInfoDefault(string url, string dbName, string username, string password, string swKey, string areaFactor)
        {
            Properties.UserSettings.Default.OdooUrl = url;
            Properties.UserSettings.Default.OdooDb = dbName;
            Properties.AppSettings.Default.SwLicenseKey = swKey;
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
                Username = username,
                Password = password,
                PersistanceType = PersistanceType.LocalComputer
            };
            cred.Save();

            OdooDefaults.OdooUser = username;
            OdooDefaults.OdooPass = password;
            OdooDefaults.OdooID = 0;

            Close();
        }
    }
}
