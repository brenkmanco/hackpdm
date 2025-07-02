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
using OdooRpcCs;
using HackPDM.Verifier;

namespace HackPDM
{
    public partial class ProfileManager : Form
    {
        OdooSettings odooSettings;
        HackSettings hackSettings;

        public ProfileManager()
        {
			this.FormClosing += this.ProfileManager_FormClosing;

            InitializeComponent();
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
              
        private void odooSettingsBtn_Click(object sender, EventArgs e)
        {
            //HackDefaults.GetDirectoriesAndEntries("D:\\pwa\\Catalog");

            odooSettings = new OdooSettings();
            odooSettings.Show();
        }

        private void OdooLoginBtn_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
        }

		private void HackSettingsBtn_Click( object sender, EventArgs e )
		{
            hackSettings = new HackSettings();
            hackSettings.Show();
		}

        private bool AbleToLogin()
        {
			List<string> errors = [];
            if (OdooDefaults.OdooID != 0) return true;

			if (!OdooClient.CorrectOdooAddress())
			{
				errors.Add("invalid odoo address or unreachable host");
			} 
			else if (!OdooClient.CorrectOdooPort())
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
