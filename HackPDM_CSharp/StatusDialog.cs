/*
 * 
 * (C) 2013 Matt Taylor
 * Date: 2/18/2013
 * 
 * This file is part of Foobar.
 * 
 * Foobar is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * Foobar is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with Foobar.  If not, see <http://www.gnu.org/licenses/>.
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
				// this is a worker thread so delegate the task
				AddStatusLineDel del = new AddStatusLineDel(AddStatusLine);
                //object[] delParam = new object[1] { Params };
                this.Invoke(del, (object)Params);
			} else {
				// we are executing in the UI thread
				ListViewItem lvItem = new ListViewItem(Params);
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
			if (cbxAutoClose.Checked == true) {
				this.Close();
			} else {
				cmdCancel.Enabled = false;
				cmdClose.Enabled = true;
			}
		}
		
		
	}
}
