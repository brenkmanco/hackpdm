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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace HackPDM
{
    /// <summary>
    /// Description of StatusDialog.
    /// </summary>
    public partial class StatusDialog : Form
    {
        public static Color ColorProcessing     { get; set; } = Color.Navy;
        public static Color ColorSkip           { get; set; } = Color.DarkGray;
        public static Color ColorFound          { get; set; } = Color.DarkGray;
        public static Color ColorSuccess        { get; set; } = Color.DarkOliveGreen;
        public static Color ColorWarning        { get; set; } = Color.Yellow;
        public static Color ColorError          { get; set; } = Color.Red;
        public static Color ColorDefaultFore    { get; set; } = Color.Black;
        public static Color ColorDefaultBack    { get; set; } = Color.White;
        
        int ErrorCount = 0;

        public static bool SkipText
        {
            get => Properties.UserSettings.Default.SkipText;
            set
            {
                Properties.UserSettings.Default.SkipText = value;
                Properties.UserSettings.Default.Save();
            }
        }

        public static int HistoryLength
        {
            get
            {
                if (field == 0)
                {
                    field = Properties.UserSettings.Default.HistoryLengthSize;
                }
                return field;
            }
            set
            {
                field = value;
                Properties.UserSettings.Default.HistoryLengthSize = value;
                Properties.UserSettings.Default.Save();
            }
        }
        public bool DoubleBuff
        {
            get => this.DoubleBuffered;
            set => this.DoubleBuffered = value;
        }
        public bool Canceled { get; private set; } = false;

        public bool ShowStatusDialog(string TitleText) {
            //var dlg = new StatusDialog(TitleText);
            this.Text = TitleText;
            this.ShowDialog();
            return this.Canceled;
        }
        
        public StatusDialog() {
            HackFileManager.queueAsyncStatus = new();
            InitializeComponent();
            ClearStatus();
        }
        
        private StatusDialog(string TitleText) : this() {
            HackFileManager.queueAsyncStatus = new();
            this.Text = TitleText;
            ClearStatus();
        }
        
        public void ClearStatus() {
            lvMessages.Clear();
            lvMessages.Columns.Add("Action",120,System.Windows.Forms.HorizontalAlignment.Left);
            lvMessages.Columns.Add("Description",1000, System.Windows.Forms.HorizontalAlignment.Left);
        }
        
        public void AddStatusLine(string Action, string Description) {
            string[] strStatusParams = [Action, Description];
            AddStatusLine(strStatusParams);
        }
        public void AddStatusLines(List<string[]> values)
        {
            AddStatusLinesInternal(values);
        }
        public void AddStatusLines(ConcurrentQueue<string[]> values)
        {
            List<string[]> batch = new(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                if (values.TryDequeue(out string[] item)) batch.Add(item);
                else break;
            }
            AddStatusLinesInternal(batch);
        }

        public void SetProgressBar(int value, int max)
        {
            SetProgressBarInternal([value, max]);
        }
        private delegate void SetProgressBarDel(int[] Params);
        private void SetProgressBarInternal(int[] Params)
        {
            if (this.InvokeRequired)
            {
                SetProgressBarDel del = new(SetProgressBarInternal);
                this.Invoke(del, (object)Params);
            }
            else
            {
                int max, value;
                value = Params[0];
                max = Params[1] > value ? Params[1] : value;

                fileCheckStatus.Maximum = max;
                fileCheckStatus.Value = value;
                ProgressText.Text = $"({(value / (float)max)*100:f2}%)\n{value} / {max}";
                SkippedLabel.Text = $"({HackFileManager.SkipCounter}) Skipped";
            }
        }
        
        private delegate void AddStatusLinesDel(List<string[]> values);
        private void AddStatusLinesInternal(List<string[]> values)
        {
            if (this.InvokeRequired)
            {
                AddStatusLinesDel del = new(AddStatusLinesInternal);
                this.Invoke(del, values);
            }
            else
            {
                lvMessages.BeginUpdate();
                int totalCount = lvMessages.Items.Count + values.Count;
                
                if (totalCount > HistoryLength)
                {
                    // 65 lvM count
                    // 100 values count
                    // 165 total 
                    // 150 history length 
                    // 15 = total - history length
                    // lvM - value = 150
                    int histOffset = totalCount - HistoryLength;
                    for (int i = 0; i < histOffset; i++)
                    {
                        if (lvMessages.Items.Count > 0)
                        {
                            lvMessages.Items.RemoveAt(0);
                        }
                    }
                }
                foreach(var item in values)
                {
                    ListViewItem lvItem = new(item);

                    // set background color, based on status action
                    switch (item[0])
                    {
                        case "PROCESSING": lvItem.ForeColor = ColorProcessing; break;
                        case "SKIP": lvItem.ForeColor = ColorSkip; break;
                        case "FOUND": lvItem.ForeColor = ColorFound; break;
                        case "SUCCESS": lvItem.ForeColor = ColorSuccess; break;
                        case "WARNING": lvItem.BackColor = ColorWarning; break;
                        case "ERROR": lvItem.BackColor = ColorError; ErrorCount++; break;
                        default: break;
                    }

                    lvMessages.Items.Add(lvItem);
                    lvMessages.EnsureVisible(lvMessages.Items.Count - 1);
                }
                lvMessages.EndUpdate();
            }
        }
        private delegate void AddStatusLineDel(string[] Params);
        private void AddStatusLine(string[] Params) {

            if (this.InvokeRequired) {

                // this is a worker thread so delegate the task to the UI thread
                AddStatusLineDel del = new(AddStatusLine);
                this.Invoke(del, (object)Params);

            } else {

                // we are executing in the UI thread
                ListViewItem lvItem = new(Params);

                // set background color, based on status action
                switch (Params[0])
                {
                    case "PROCESSING": lvItem.ForeColor = ColorProcessing; break;
                    case "SKIP": lvItem.ForeColor = ColorSkip; break;
                    case "FOUND": lvItem.ForeColor = ColorFound; break;
                    case "SUCCESS": lvItem.ForeColor = ColorSuccess; break;
                    case "WARNING": lvItem.BackColor = ColorWarning; break;
                    case "ERROR": lvItem.BackColor = ColorError; ErrorCount++; break;
                    default: break;
                }
                lvMessages.Items.Add(lvItem);
                lvMessages.EnsureVisible(lvMessages.Items.Count - 1);

            }

        }
        
        private void CmdCancelClick(object sender, EventArgs e) {
            Canceled = true;
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

        private void StatusSettings_Click(object sender, EventArgs e)
        {
            new StatusSettings().Show();
        }

        private void lvMessages_DrawItem(object sender, DrawListViewItemEventArgs e)
        {

        }

        private void lvMessages_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (lvMessages.Items.Count > HistoryLength)
            {
                lvMessages.Items.RemoveAt(0);
            }
        }
    }
}
