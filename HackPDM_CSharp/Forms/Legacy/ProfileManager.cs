/*
 * 
 * (C) 2013 Matt Taylor
 * Date: 2/18/2013
 * 
 * This file is part of HackPDM.
 * 
 * HackPDM is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * HackPDM is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with HackPDM.  If not, see <http://www.gnu.org/licenses/>.
 * 
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using HackPDM.Forms.Hack;
using System.Runtime.Remoting.Messaging;

namespace HackPDM
{
    public partial class ProfileManager : Form
    {
        private string strDefaultProfileId;
        DataTable dtProfiles;
        BindingSource bsProfiles = [];
        OdooSettings odooSettings;
        HackSettings hackSettings;

        public ProfileManager()
        {
			this.FormClosing += this.ProfileManager_FormClosing;

            InitializeComponent();
            InitView();
        }

		private void ProfileManager_FormClosing( object sender, FormClosingEventArgs e ) 
        {
            if (e.CloseReason is CloseReason.UserClosing or CloseReason.FormOwnerClosing or CloseReason.MdiFormClosing or CloseReason.ApplicationExitCall)
            {
                this.DialogResult = DialogResult.Cancel;
                Application.ExitThread();
                Application.Exit();
            }
        }

		public ProfileManager(List<string> messages) : this()
        {
            foreach (string message in messages)
            {
                var listItem = HackFileManager.EmptyListItem(ProfileManStatusList);

                listItem.SubItems["Status"].Text = "ERROR";
                listItem.SubItems["Message"].Text = message;

                ProfileManStatusList.Items.Add(listItem);
            }
        }


        private void InitView()
        {

            // clear the list
            cboProfile.Items.Clear();

            // load profile strings
            //string strTest = Application.LocalUserAppDataPath;
            strDefaultProfileId = Properties.UserSettings.Default.usetDefaultProfile;
            string strXmlProfiles = Properties.UserSettings.Default.usetProfiles;


            if (strXmlProfiles == "")
            {
                dtProfiles = CreateProfileTable();
            }
            else
            {
                StringReader reader = new(strXmlProfiles);
                dtProfiles = new DataTable("profiles");
                dtProfiles.ReadXmlSchema(reader);
                reader = new StringReader(strXmlProfiles);
                dtProfiles.ReadXml(reader);
            }

            bsProfiles.DataSource = dtProfiles;

            foreach (DataRow dr in dtProfiles.Rows)
            {
                cboProfile.Items.Add(dr["PfGuid"]);
            }

            //cboProfile.DataBindings.Add("ValueMember", bsProfiles, "PfGuid");
            //cboProfile.DataBindings.Add("Text", bsProfiles, "PfName");

            txtPfName.DataBindings.Add("Text", bsProfiles, "PfName");
            txtDbServ.DataBindings.Add("Text", bsProfiles, "DbServ");
            txtDbPort.DataBindings.Add("Text", bsProfiles, "DbPort");
            txtDbUser.DataBindings.Add("Text", bsProfiles, "DbUser");
            txtDbPass.DataBindings.Add("Text", bsProfiles, "DbPass");
            txtDbName.DataBindings.Add("Text", bsProfiles, "DbName");
            txtDavServ.DataBindings.Add("Text", bsProfiles, "DavServ");
            txtDavPort.DataBindings.Add("Text", bsProfiles, "DavPort");
            txtDavUser.DataBindings.Add("Text", bsProfiles, "DavUser");
            txtDavPass.DataBindings.Add("Text", bsProfiles, "DavPass");
            txtDavPath.DataBindings.Add("Text", bsProfiles, "DavPath");
            txtFsRoot.DataBindings.Add("Text", bsProfiles, "FsRoot");
            txtUsername.DataBindings.Add("Text", bsProfiles, "Username");
            txtPassword.DataBindings.Add("Text", bsProfiles, "Password");

            //cboProfile.DataSource = dtProfiles;
            //cboProfile.ValueMember = "strPfGuid";
            //cboProfile.DisplayMember = "strPfName";

            //SelectRecord(strDefaultProfileId);
            cboProfile.SelectedIndex = bsProfiles.Position;

        }


        private DataTable CreateProfileTable()
        {

            DataTable dtProfile = new("profiles");

            dtProfile.Columns.Add("PfGuid", Type.GetType("System.String"));
            dtProfile.Columns.Add("PfName", Type.GetType("System.String"));
            dtProfile.Columns.Add("DbServ", Type.GetType("System.String"));
            dtProfile.Columns.Add("DbPort", Type.GetType("System.String"));
            dtProfile.Columns.Add("DbUser", Type.GetType("System.String"));
            dtProfile.Columns.Add("DbPass", Type.GetType("System.String"));
            dtProfile.Columns.Add("DbName", Type.GetType("System.String"));
            dtProfile.Columns.Add("DavServ", Type.GetType("System.String"));
            dtProfile.Columns.Add("DavPort", Type.GetType("System.String"));
            dtProfile.Columns.Add("DavUser", Type.GetType("System.String"));
            dtProfile.Columns.Add("DavPass", Type.GetType("System.String"));
            dtProfile.Columns.Add("DavPath", Type.GetType("System.String"));
            dtProfile.Columns.Add("FsRoot", Type.GetType("System.String"));
            dtProfile.Columns.Add("Username", Type.GetType("System.String"));
            dtProfile.Columns.Add("Password", Type.GetType("System.String"));

            strDefaultProfileId = Guid.NewGuid().ToString();

            //dtProfile.Rows.Add(strDefaultProfileId,
            //    "testing",
            //    "192.168.1.15",
            //    "5432",
            //    "tcp_user",
            //    "tcp_user",
            //    "hackpdm",
            //    "C:\\pwa",
            //    "demo",
            //    "demo");

            dtProfile.Rows.Add(strDefaultProfileId,
                "New",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "");

            return dtProfile;

        }


        void CmdTestClick(object sender, EventArgs e)
        {

            // inform the user and die
            MessageBox.Show("This functionality not yet implemented.",
                "Settings Test Result",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;

        }


        void CmdCommitClick(object sender, EventArgs e)
        {
            StringWriter writer = new();
            dtProfiles.WriteXml(writer);
            Properties.UserSettings.Default.usetProfiles = writer.ToString();
            Properties.UserSettings.Default.usetDefaultProfile = strDefaultProfileId;
            Properties.UserSettings.Default.Save();
            this.Close();
        }


        void CboProfileSelectedIndexChanged(object sender, EventArgs e)
        {
            string strPfGuid = cboProfile.Text;
            SelectRecord(strPfGuid);
        }


        void SelectRecord(string strPfGuid)
        {
            int foundIndex = bsProfiles.Find("PfGuid", strPfGuid);
            bsProfiles.Position = foundIndex;
        }


        void CmdNewClick(object sender, EventArgs e)
        {

            string strGuid = Guid.NewGuid().ToString();
            dtProfiles.Rows.Add(strGuid,
                "New",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "");

            SelectRecord(strGuid);

        }

        private void odooSettingsBtn_Click(object sender, EventArgs e)
        {
            //HackDefaults.GetDirectoriesAndEntries("D:\\pwa\\Catalog");

            odooSettings = new OdooSettings();
            odooSettings.Show();
        }

        private void OdooLoginBtn_Click(object sender, EventArgs e)
        {
            if (AbleToLogin()) new HackFileManager().Show();
            else this.DialogResult = DialogResult.OK;
        }

		private void HackSettingsBtn_Click( object sender, EventArgs e )
		{
            hackSettings = new HackSettings();
            hackSettings.Show();
		}

        private bool AbleToLogin()
        {
			List<string> errors = new();
			if (HackFileManager.CorrectOdooAddress())
			{
				errors.Add("invalid odoo address or unreachable host");
			} 
			else if (HackFileManager.CorrectOdooPort())
			{
				errors.Add("invalid odoo port or server is down");
			}
			else
			{
				errors.Add("invalid odoo credentials");
			}
			if (errors.Count > 0)
			{
                foreach (string message in errors)
                {
                    var listItem = HackFileManager.EmptyListItem(ProfileManStatusList);

                    listItem.SubItems["Status"].Text = "ERROR";
                    listItem.SubItems["Message"].Text = message;

                    ProfileManStatusList.Items.Add(listItem);
                }
                return false;
            }
            return true;
		}
	}
}
