using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.WinUI.UI.Controls;

using HackPDM.ClientUtils;
using HackPDM.Data;
using HackPDM.Extensions.Controls;
using HackPDM.Forms.Hack;
using HackPDM.Helper;
using HackPDM.Src.ClientUtils.Types;
using HackPDM.Src.Helper.Xaml;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using Setting = HackPDM.Properties.Settings;

using MessageBox = System.Windows.Forms.MessageBox;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;
using MessageBoxDefaultButton = System.Windows.Forms.MessageBoxDefaultButton;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM.Forms.Settings;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class StatusDialog : Page
{
    public Window? ParentWindow { get; set; }

    public ObservableCollection<BasicStatusMessage> OStatus { get; internal set; } = [];
    public ObservableCollection<BasicStatusMessage> OInfo { get; internal set; } = [];
    public ObservableCollection<BasicStatusMessage> OError { get; internal set; } = [];
    public static Brush ColorProcessing { get; set; } = StorageBox.BrushDarkBlue;
    public static Brush ColorSkip { get; set; } = StorageBox.BrushDarkGray;
    public static Brush ColorFound { get; set; } = StorageBox.BrushDarkGray;
    public static Brush ColorSuccess { get; set; } = StorageBox.BrushDarkOliveGreen;
    public static Brush ColorWarning { get; set; } = StorageBox.BrushMustardYellow;
    public static Brush ColorError { get; set; } = StorageBox.BrushDarkRed;
    public static Brush ColorDefaultFore { get; set; } = StorageBox.BrushBlack;
    public static Brush ColorDefaultBack { get; set; } = StorageBox.BrushWhite;

    private delegate void SetProgressBarDel(int[] @params);
    private delegate void AddStatusLinesDel(List<(StatusMessage action, string description)> values);
    
	int _errorCount = 0;
    public static bool? SkipText
    {
        get
        {
            field ??= Setting.Get("SkipText", field);
            return field;
        }
        set
        {
            field = value;
            Setting.Set("SkipText", field);
        }
    }
    public static int? HistoryLength
    {
        get
        {
            field ??= Setting.Get("HistoryLength", field) ?? 1000;
            return field;
        }
        set
        {
            field = value;
            Setting.Set("HistoryLength", field);
        }
    }
    public bool DoubleBuff { get; set; } = true;
    public bool Canceled { get; private set; } = false;
    public bool HasLoaded { get; set; } = false;
	internal bool IsInProcess { get; set; }

    public bool ShowStatusDialog(string titleText)
    {
        //var dlg = new StatusDialog(TitleText);

        ParentWindow ??= WindowHelper.CreateWindowPage<StatusDialog>();
        return this.Canceled;
    }
    public async Task<bool> ShowWait(string titleText)
    {
        return await AsyncHelper.WaitUntil(() => ParentWindow?.Visible ?? true, 100, 10000);
    }

    public StatusDialog()
    {
        HackFileManager.QueueAsyncStatus = new();
        InitializeComponent();
        ClearStatus();
        this.Loaded += new((s, e)=> HasLoaded = true);
		ParentWindow?.AppWindow.Closing += AppWindow_Closing;
    }

	private async void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
	{
		args.Cancel = DialogResult.OK != MessageBox.Show(
			"There are items still processing..\nWould you like to continue and close the window and operations?", 
			"Cancel Operation?",
			MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
		if (args.Cancel)
		{
			if (HackFileManager.statusToken is { } cts)
			{
				await cts.CancelAsync();
			}
			IsInProcess = false;
		}
	}

	private StatusDialog(string titleText) : this()
    {
        HackFileManager.QueueAsyncStatus = new();
        ParentWindow?.Title = titleText;
        ClearStatus();
    }
    public void ClearStatus()
    {

    }
    public void AddStatusLine(StatusMessage action, string description)
    {
        AddStatusLine((action, description));
    }

    public void AddStatusLines(ConcurrentQueue<(StatusMessage action, string description)> values)
    {
        //ObservableQueue<(StatusMessage, string)> batch = new(values.Count);
        //for (int i = 0; i < values.Count; i++)
        //{
        //    if (values.TryDequeue(out (StatusMessage action, string description) item)) batch.Enqueue(item);
        //    else break;
        //}
        AddStatusLinesInternal(values);
    }

    public void SetProgressBar(int value, int max)
    {
        SetProgressBarInternal([value, max]);
    }
    private void SetProgressBarInternal(int[] @params)
    {
        this.DispatcherQueue.ExecuteUI(()=>
        {
            int max, value;
            value = @params[0];
            max = @params[1] > value ? @params[1] : value;

            fileCheckStatus.Maximum = max;
            fileCheckStatus.Value = value;
            ProgressText.Text = $"({(value / (float)max) * 100:f2}%)\n{value} / {max}";
            SkippedLabel.Text = $"({HackFileManager.SkipCounter}) Skipped";
        });
    }

    private void AddStatusLinesInternal(ConcurrentQueue<(StatusMessage action, string description)> values)
    {
		this.DispatcherQueue.ExecuteUI(()=>
        {
            while (values.TryDequeue(out var value))
            {
				GetDataGrid(value.action, out var collection, out var messageLog);
				
                var lvItem = GridHelp.EmptyListItem<BasicStatusMessage>(messageLog);
				lvItem.Status = value.action;
				lvItem.Message = value.description;
				//ColorizeStatus(item, lvItem);
				// set background color, based on status action

				collection.Add(lvItem);
			}

			RemoveExcessLines(StatusList);
			RemoveExcessLines(InfoList);
			RemoveExcessLines(ErrorList);
		});
    }
	private void RemoveExcessLines(DataGrid grid)
	{
		object locker = new();
		lock (locker)
		{
			var collection = grid.ItemsSource as ObservableCollection<BasicStatusMessage>;
			if (collection == null) return;

			int totalCount = collection!.Count;
			
			if (totalCount > (HistoryLength ?? 1000))
			{
				// 65 lvM count
				// 100 values count
				// 165 total 
				// 150 history length 
				// 15 = total - history length
				// lvM - value = 150
				int histOffset = (totalCount - HistoryLength) ?? 1000;
				for (int i = 0; i < histOffset; i++)
				{
					if (collection.Count > 0)
					{
						collection.RemoveAt(0);
					}
				}
			}
			grid.ScrollIntoView(collection.LastOrDefault(), null);
		}
	}
    private delegate void AddStatusLineDel(string[] @params);
    private void AddStatusLine((StatusMessage action, string description) statusMessage)
    {
        this.DispatcherQueue.ExecuteUI(()=>
        {
            // we are executing in the UI thread
            GetDataGrid(statusMessage.action, out var collection, out var messageLog);
            var lvItem = GridHelp.EmptyListItem<BasicStatusMessage>(messageLog);
            lvItem.Status = statusMessage.action;
            lvItem.Message = statusMessage.description;
            // set background color, based on status action
            collection.Add(lvItem);
		});
    }
    
    private void ColorizeStatus((StatusMessage action, string description) values, ListViewItem item)
    {
        switch (values.action)
        {
            case StatusMessage.PROCESSING: item.Foreground = ColorProcessing; break;
            case StatusMessage.SKIP: item.Foreground = ColorSkip; break;
            case StatusMessage.FOUND: item.Foreground = ColorFound; break;
            case StatusMessage.SUCCESS: item.Foreground = ColorSuccess; break;
            case StatusMessage.WARNING: item.Background = ColorWarning; break;
            case StatusMessage.ERROR: item.Background = ColorError; _errorCount++; break;
            default: break;
        }
    }
    private DataGrid GetList(StatusMessage action) => action switch
    {
        StatusMessage.PROCESSING    => StatusList,
        StatusMessage.SUCCESS       => StatusList,
        StatusMessage.SKIP          => InfoList,
        StatusMessage.FOUND         => InfoList,
        StatusMessage.INFO          => InfoList,
        StatusMessage.OTHER         => InfoList,
        StatusMessage.WARNING       => ErrorList,
        StatusMessage.ERROR         => ErrorList,
        _                           => InfoList,
    };
    private void GetDataGrid(StatusMessage action, out ObservableCollection<BasicStatusMessage> collection, out DataGrid? dataGrid)
    {
        switch (action)
        {
            case StatusMessage.PROCESSING:
            case StatusMessage.SUCCESS:
                collection = OStatus;
                dataGrid = StatusList;
                break;
            case StatusMessage.SKIP:
            case StatusMessage.FOUND:
            case StatusMessage.INFO:
            case StatusMessage.OTHER:
                collection = OInfo;
                dataGrid = InfoList;
                break;
            case StatusMessage.WARNING:
            case StatusMessage.ERROR:
                collection = OError;
                dataGrid = ErrorList;
                break;
            default:
                collection = OInfo;
                dataGrid = InfoList;
                break;
        }
    }
    private void CmdCancelClick()
    {
        Canceled = true;
    }

    void CmdCloseClick()
    {
		Canceled = true;
		ParentWindow?.Close();
	}

    public void OperationCompleted()
    {
        if (_errorCount != 0)
            AddStatusLine(StatusMessage.ERROR, $"Encountered {_errorCount} errors");
        else if (cbxAutoClose.IsEnabled == true)
            CmdCloseClick();
        cmdCancel.IsEnabled = false;
        cmdClose.IsEnabled = true;
    }

    private void StatusSettings_Click(object sender, EventArgs e)
    {
        //var page = InstanceManager.GetAPage<StatusSettings>();
        var window = WindowHelper.CreateWindowPage<StatusSettings>();
    }

	internal void SetDownloaded(object downloadBytes)
	{
		throw new NotImplementedException();
	}

	internal void SetTotalDownloaded(object sessionDownloadBytes)
	{
		throw new NotImplementedException();
	}
}
public struct StatusData
{
    public static StatusData StaticData = new();
    public static long SessionDownloadBytes;
    public int totalProcessed;
    public int SkipCounter;
    public int ProcessCounter;
    public int MaxCount;
    public long DownloadBytes;
    public StatusData() 
    { 
    }
}