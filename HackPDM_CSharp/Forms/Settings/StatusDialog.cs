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
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using HackPDM.ClientUtils;
using HackPDM.ClientUtils.Queue;
using HackPDM.Extensions.Form;

using ListView = System.Windows.Forms.ListView;

namespace HackPDM.Forms.Settings
{
    /// <summary>
    /// Description of StatusDialog.
    /// </summary>
    public partial class StatusDialog : SingletonForm<StatusDialog>
    {
        public static   StatusData StaticData { get; set; } = StatusData.StaticData;
        public static   StatusDialog Dialog { get; set; }
        public          FrozenDictionary<StatusMessage, ListDetails> StatConfig;
        public          FrozenDictionary<ListView, GroupLists> RefDetails;

        public readonly ListView[] Views;
        readonly ColumnHeader ActionDefault = new()
        {
            Name = "Action",
            Text = "Status",
            Width = 120,
            TextAlign = HorizontalAlignment.Left,
        };
        readonly ColumnHeader DescriptionDefault = new()
        {
            Name = "Description",
            Text = "Description",
            Width = 1000,
            TextAlign = HorizontalAlignment.Left,
        };
        

        public static bool SkipText
        {
            get => Properties.UserSettings.Default.SkipText;
            set
            {
                Properties.UserSettings.Default.SkipText = value;
                Properties.UserSettings.Default.Save();
            }
        }
        public static int MaxHistoryLength
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
        public bool Canceled 
        { 
            get; 
            private set; 
        } = false;
        public bool IsLoaded 
        { 
            get; 
            set; 
        } = false;
        
        public bool ShowStatusDialog(string TitleText) {
            //var dlg = new StatusDialog(TitleText);
            this.Text = TitleText;
            this.ShowDialog();
            return this.Canceled;
        }
        public async Task<bool> ShowWait(string titleText)
        {
            this.Text = titleText;
            this.Show();
            return await AsyncHelper.WaitUntil(() => this.Visible, 100, 10000);
        }

        public StatusDialog() 
        {
            InitializeComponent();
            Views = [
                StatusList,
                InfoList,
                ErrorList,
            ];

            InitLists();

            ClearStatuses();
            HackFileManager.QueueAsyncStatus = new();

            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            this.SetFormTheme(ProfileManager.MyTheme);
            this.UpdateStyles();
            this.Load += new EventHandler(FormLoaded);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Canceled = true;
            base.OnFormClosing(e);
        }
        
        private StatusDialog(string TitleText) : this() 
        {
            this.Text = TitleText;
            ClearStatus();
        }
        private void FormLoaded(object sender, EventArgs e)
        {
            IsLoaded = true;
        }
        private void InitLists()
        {
            ListDetails listProcess = new(StatusList,   new StatusConfig(StatusList,    Color.Navy, Color.White, "PROCESSING"));
            ListDetails listSuccess = new(StatusList,   new StatusConfig(StatusList,    Color.DarkOliveGreen, Color.White, "SUCCESS"));
            ListDetails listDefault = new(StatusList,   new StatusConfig(StatusList,    Color.Black, Color.White, "..."));
            ListDetails listInfo    = new(InfoList,     new StatusConfig(InfoList,      Color.Black, Color.White, "..."));
            ListDetails listSkip    = new(InfoList,     new StatusConfig(InfoList,      Color.DarkGray, Color.White, "SKIP"));
            ListDetails listFound   = new(InfoList,     new StatusConfig(InfoList,      Color.DarkGray, Color.White, "FOUND"));
            ListDetails listWarn    = new(ErrorList,    new StatusConfig(ErrorList,     Color.OrangeRed, Color.White, "WARNING"));
            ListDetails listError   = new(ErrorList,    new StatusConfig(ErrorList,     Color.White, Color.Red, "ERROR"));

            StatConfig = new Dictionary<StatusMessage, ListDetails>
            {

                {StatusMessage.PROCESSING,  listProcess },
                {StatusMessage.SUCCESS,     listSuccess },
                {StatusMessage.OTHER,       listDefault },
                {StatusMessage.INFO,        listInfo    },      
                {StatusMessage.SKIP,        listSkip    },
                {StatusMessage.FOUND,       listFound   },
                {StatusMessage.WARNING,     listWarn    },
                {StatusMessage.ERROR,       listError   },
            }.ToFrozenDictionary();

            RefDetails = new Dictionary<ListView, GroupLists>
            {
                {StatusList,    new(StatusList, [listProcess, listSuccess, listDefault], 10)},
                {InfoList,      new(InfoList,   [listSkip, listInfo, listFound], 10)},
                {ErrorList,     new(ErrorList,  [listWarn, listError], 10)},
            }.ToFrozenDictionary();

            foreach (var list in Views)
            {
                list.VirtualMode = true;
                list.RetrieveVirtualItem    += new RetrieveVirtualItemEventHandler  (List_RetrieveVirtualItem);
                list.CacheVirtualItems      += new CacheVirtualItemsEventHandler    (List_CacheVirtualItems);
                list.SearchForVirtualItem   += new SearchForVirtualItemEventHandler (List_SearchForVirtualItem);
            }
        }
        public void ClearStatus() => ClearStatuses();
        public void ClearStatus(ListView list)
        {
            list.Clear();
            list.Columns.AddRange([(ColumnHeader)ActionDefault.Clone(), (ColumnHeader)DescriptionDefault.Clone()]);
        }
        public void ClearStatuses()
        {
            foreach (var list in Views)
            {
                ClearStatus(list);
            }
        }
        public void AddStatusLine(StatusMessage Action, string Description) {
            (StatusMessage, string) strStatusParams = (Action, Description);
            AddStatusLine(strStatusParams);
        }
        public void AddStatusLines(List<(StatusMessage, string)> values)
        {
            AddStatusLinesInternal(values);
        }
        public void AddStatusLines(ConcurrentQueue<(StatusMessage, string)> values)
        {
            List<(StatusMessage, string)> batch = new(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                if (values.TryDequeue(out (StatusMessage, string) item)) batch.Add(item);
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
                SkippedLabel.Text = $"({StatusData.StaticData.SkipCounter}) Skipped";
            }
        }
        private delegate void SetDownloadedDel(long size);
        public void SetTotalDownloaded(long size)
        {
            if (this.InvokeRequired) this.Invoke(new SetDownloadedDel(SetTotalDownloaded), size);
            else
            {
                TotalDownload.Text = $"Total Downloaded: {FileOperations.FileSizeReformat(size, true)}";
            }
        }
        public void SetDownloaded(long size)
        {
            if (this.InvokeRequired) this.Invoke(new SetDownloadedDel(SetDownloaded), size);
            else
            {
                Downloaded.Text = $"Downloaded: {FileOperations.FileSizeReformat(size, true)}";
            }
        }
        
        private delegate void AddStatusLinesDel(List<(StatusMessage, string)> values);
        private void AddStatusLinesInternal(List<(StatusMessage action, string description)> values)
        {
            if (this.InvokeRequired)
            {
                AddStatusLinesDel del = new(AddStatusLinesInternal);
                this.Invoke(del, values);
            }
            else
            {
                foreach (var item in values)
                {
                    // set background color, based on status action
                    ListDetails config = GetStatus(item.action);

                    config.Parent.Invalidated = true;
                    var listItem = config.Config.SetConfig(Enum.GetName(typeof(StatusMessage), item.action), item.description);
                    int totalCount = config.List.Items.Count + values.Count;

                    config.Parent.QueueValues.Enqueue(item);
                }
                foreach (GroupLists group in GroupLists.AllGroups)
                {
                    if (!group.Invalidated) continue;

                    group.ListCache = [];
                    group.List.Invalidate();
                    group.List.VirtualListSize = group.QueueValues.Count;
                    group.List.EnsureVisible(group.List.VirtualListSize - 1);
                    group.Invalidated = false;
                }
            }
        }
        private delegate void AddStatusLineDel((StatusMessage action, string description) message);
        private void AddStatusLine((StatusMessage action, string description) message) 
        {
            if (this.InvokeRequired) 
            {
                // this is a worker thread so delegate the task to the UI thread
                AddStatusLineDel del = new(AddStatusLine);
                this.Invoke(del, (object)message);

            } else 
            {
                // we are executing in the UI thread
                ListDetails config = GetStatus(message.action);
                config.Parent.QueueValues.Enqueue(message);

                config.Parent.ListCache = [];
                config.List.Invalidate();
                config.List.VirtualListSize = config.Parent.QueueValues.Count;
                config.List.EnsureVisible(config.List.VirtualListSize - 1);
                // ListViewItem lv = config.List.FindItemWithText(null, false, Math.Max(0, config.Parent.QueueValues.Count-1));
                //if (lv == null) return;
                //config.List.EnsureVisible(lv.Index);
            }
        }
        public ListDetails GetStatus(StatusMessage message) => StatConfig[message];
        public StatusMessage GetStatusMessage(string errorMessage) => errorMessage.ToUpper() switch
        {
            "PROCESSING"    => StatusMessage.PROCESSING,
            "SKIP"          => StatusMessage.SKIP,
            "INFO"          => StatusMessage.INFO,
            "FOUND"         => StatusMessage.FOUND,
            "SUCCESS"       => StatusMessage.SUCCESS,
            "WARNING"       => StatusMessage.WARNING,
            "ERROR"         => StatusMessage.ERROR,
            _               => StatusMessage.OTHER
        };
        private void CmdCancelClick(object sender, EventArgs e) 
        {
            Canceled = true;
            this.Close();
        }
        
        void CmdCloseClick(object sender, EventArgs e)
        {
            this.Close();
        }
        
        public void OperationCompleted() {
            if (StatusData.StaticData.ErrorCount != 0)
                AddStatusLine(StatusMessage.ERROR, String.Format("Encountered {0} errors", StatusData.StaticData.ErrorCount));
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

        private void Lists_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            ListView lv = sender as ListView;

            if (lv.Items.Count > MaxHistoryLength)
            {
                lv.Items.RemoveAt(0);
            }
        }

        private void StatusDialog_Load(object sender, EventArgs e)
        {

        }



        private void List_RetrieveVirtualItem   (object sender, RetrieveVirtualItemEventArgs e)
        {
            try
            {

                ListView list = sender as ListView;
                GroupLists details = RefDetails[list];
                // first is 5
                // index is 44
                // count is 45
                // it would be at 50
                if (e.ItemIndex >= details.FirstItemIndex && e.ItemIndex < details.FirstItemIndex + details.ListCache.Length)
                {
                    // cache hit
                    e.Item = details.ListCache[e.ItemIndex - details.FirstItemIndex];
                }
                else
                {
                    // cache miss
                    if (e.ItemIndex >= 0 && e.ItemIndex < details.QueueValues.Count)
                    {
                        (StatusMessage action, string description) = details.QueueValues[e.ItemIndex];
                        ListDetails listDetails = GetStatus(action);
                        e.Item = listDetails.Config.SetConfig(Enum.GetName(typeof(StatusMessage), action), description);
                    }
                    else
                    {
                        e.Item = new ListViewItem(["", ""]);
                    }
                }
            }
            catch (Exception ex)
            {
                
            }
            e.Item ??= new ListViewItem(["", ""]);
        }
        private void List_CacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
        {
            ListView list = sender as ListView;
            GroupLists details = RefDetails[list];

            if (e.StartIndex >= details.FirstItemIndex && e.EndIndex <= details.FirstItemIndex + details.ListCache.Length)
            {
                return;
            }
            details.FirstItemIndex = e.StartIndex;
            int actualStart = Math.Max(0, Math.Min(e.StartIndex, details.QueueValues.Count - 1));
            int actualEnd = Math.Max(-1, Math.Min(e.EndIndex, details.QueueValues.Count - 1));
            int length = actualEnd - actualStart + 1;
            details.ListCache = new ListViewItem[length];

            for (int i = 0; i < length; i++)
            {
                (StatusMessage action, string description) = details.QueueValues[i + actualStart];
                ListDetails listDetails = GetStatus(action);
                details.ListCache[i] = listDetails.Config.SetConfig(Enum.GetName(typeof(StatusMessage), action), description);
            }
        }
        private void List_SearchForVirtualItem(object sender, SearchForVirtualItemEventArgs e)
        {
            ListView list = sender as ListView;
            GroupLists details = RefDetails[list];
            e.Index = Math.Max(1, details.QueueValues.Count - 1);
        }

    }
    public class StatusData
    {
        internal static StatusData StaticData = null;
        internal static long SessionDownloadBytes = 0;
        internal int ProcessCounter = 0;
        internal int totalProcessed = 0;
        internal int MaxCount = 0;
        internal long DownloadBytes = 0;
        internal int ErrorCount = 0;
        internal int SkipCounter = 0;

        static StatusData()
        {
            if (StaticData is null)
            {
                StaticData = new();
            }
        }
        public StatusData()
        {
        }
    }
    public struct StatusConfig(ListView list, Color foreground, Color background, string statusMessage)
    {
        public readonly ListView List { get; } = list;
        public readonly Color Foreground { get; } = foreground;
        public readonly Color Background { get; } = background;
        public readonly string StatusAction { get; } = statusMessage;
        

        public ListViewItem SetConfig(params string[] param)
        {
            var item = new ListViewItem(param) 
            { 
                BackColor=Background, 
                ForeColor=Foreground, 
            };
            //item.SubItems[0].Text = StatusAction;
            return item;
        }
    }
    public class ListDetails
    {
        public GroupLists Parent { get; internal set; }
        public ListView List { get; }
        public StatusConfig Config { get; }
        public int HistoryLength => Parent?.HistoryLength ?? 0;

        public ListDetails(ListView list, StatusConfig config)
        {
            List = list;
            Config = config;

            List.VirtualMode = true;
            List.VirtualListSize = HistoryLength;
        }
    }
    public class GroupLists
    {
        public static GroupLists[] AllGroups = [];
        public IndexedQueue<(StatusMessage action, string description)> QueueValues;
        public ListView List { get; }
        public ListDetails[] Views { get; }
        public ListViewItem[] ListCache { get; set; } = [];
        public int HistoryLength { get; set; } = 0;
        public int FirstItemIndex { get; set; } = 0;
        public bool Invalidated { get; set; } = false;

        public GroupLists(ListView list, ListDetails[] views, int historyLengthByTwoPower = 10)
        {
            List = list;
            Views = views;
            HistoryLength = Convert.ToInt32(Math.Pow(2, historyLengthByTwoPower));
            //ListCache = new ListViewItem[HistoryLength];
            QueueValues = new(HistoryLength, false);
            foreach (var view in Views)
            {
                view.Parent = this;
            }
            AllGroups = [.. AllGroups, this];
        }
    }
}
