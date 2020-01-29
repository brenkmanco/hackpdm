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
using System.Drawing;
using System.Windows.Forms;

namespace HackPDM
{
    /// <summary>
    /// Description of StatusDialog.
    /// </summary>
    public partial class StatusDialog : Form
    {
        
        private bool blnCanceled = false;
        int ErrorCount = 0;
        
        public bool Canceled {
            get { return blnCanceled; }
            private set { blnCanceled = value; }
        }
        
        public bool ShowStatusDialog(string TitleText) {
            //var dlg = new StatusDialog(TitleText);
            this.Text = TitleText;
            this.ShowDialog();
            return this.Canceled;
        }
        
        public StatusDialog() {
            InitializeComponent();
        }
        
        private StatusDialog(string TitleText) {
            InitializeComponent();
            this.Text = TitleText;
        }
        
        public void ClearStatus() {
            lvMessages.Clear();
            lvMessages.Columns.Add("Action",120,System.Windows.Forms.HorizontalAlignment.Left);
            lvMessages.Columns.Add("Description",460, System.Windows.Forms.HorizontalAlignment.Left);
        }
        
        public void AddStatusLine(string Action, string Description) {
            string[] strStatusParams = new String[2] {Action, Description};
            AddStatusLine(strStatusParams);
        }
        
        private delegate void AddStatusLineDel(string[] Params);
        private void AddStatusLine(string[] Params) {

            if (this.InvokeRequired) {

                // this is a worker thread so delegate the task to the UI thread
                AddStatusLineDel del = new AddStatusLineDel(AddStatusLine);
                this.Invoke(del, (object)Params);

            } else {

                // we are executing in the UI thread
                ListViewItem lvItem = new ListViewItem(Params);

                // set background color, based on status action
                if (Params[0] == "WARNING")
                    lvItem.BackColor = Color.Yellow;
                else if (Params[0] == "ERROR")
                {
                    lvItem.BackColor = Color.Red;
                    ErrorCount++;
                }
                lvMessages.Items.Add(lvItem);
                lvMessages.EnsureVisible(lvMessages.Items.Count - 1);

            }

        }
        
        private void CmdCancelClick(object sender, EventArgs e) {
            blnCanceled = true;
            this.Close();
        }
        
        void CmdCloseClick(object sender, EventArgs e)
        {
            this.Close();
        }
        
        public void OperationCompleted() {
            if (ErrorCount != 0)
                AddStatusLine("ERROR", String.Format("Encountered {0} errors", ErrorCount));
            else if (cbxAutoClose.Checked == true)
                this.Close();
            cmdCancel.Enabled = false;
            cmdClose.Enabled = true;
        }


    }
}
