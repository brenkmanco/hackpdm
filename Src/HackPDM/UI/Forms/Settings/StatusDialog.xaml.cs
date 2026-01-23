using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HackPDM.Abstractions;
using HackPDM.Core;
using HackPDM.Core.Hack;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo.FormTransport;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;
using HackPDM.UI.Data;
using HackPDM.UI.Forms.FormTransport;
using HackPDM.UI.Forms.Hack;
using HackPDM.UI.Forms.Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Brush = Microsoft.UI.Xaml.Media.Brush;
using DataGrid = CommunityToolkit.WinUI.UI.Controls.DataGrid;
using ListViewItem = Microsoft.UI.Xaml.Controls.ListViewItem;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM.UI.Forms.Settings;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class StatusDialog : Page
{
    public Window? ParentWindow { get; set; }

    public ObservableCollection<BasicStatusMessage> OStatus { get; internal set; } = [];
    public ObservableCollection<BasicStatusMessage> OInfo { get; internal set; } = [];
    public ObservableCollection<BasicStatusMessage> OError { get; internal set; } = [];
    public static Brush ColorProcessing { get; set; } = UIStorage.BrushDarkBlue;
    public static Brush ColorSkip { get; set; } = UIStorage.BrushDarkGray;
    public static Brush ColorFound { get; set; } = UIStorage.BrushDarkGray;
    public static Brush ColorSuccess { get; set; } = UIStorage.BrushDarkOliveGreen;
    public static Brush ColorWarning { get; set; } = UIStorage.BrushMustardYellow;
    public static Brush ColorError { get; set; } = UIStorage.BrushDarkRed;
    public static Brush ColorDefaultFore { get; set; } = UIStorage.BrushBlack;
    public static Brush ColorDefaultBack { get; set; } = UIStorage.BrushWhite;
    
	private int _errorCount = 0;
    
    public static bool? SkipText
    {
        get
        {
            field ??= HackDefaults.Instance?.SettingsProvider?.Get("SkipText", field);
            return field;
        }
        set
        {
            field = value;
            HackDefaults.Instance?.SettingsProvider?.Set("SkipText", field);
        }
    }
    public static int? HistoryLength
    {
        get
        {
            field ??= HackDefaults.Instance?.SettingsProvider?.Get("HistoryLength", field) ?? 100000;
            return field;
        }
        set
        {
            field = value;
            HackDefaults.Instance?.SettingsProvider?.Set("HistoryLength", field);
        }
    }
    public bool DoubleBuff { get; set; } = true;
    public bool Canceled { get; private set; }
    public bool HasLoaded { get; set; }
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
		this.Unloaded += StatusDialog_Unloaded;
    }

	private async void StatusDialog_Unloaded(object sender, RoutedEventArgs e)
	{
        //if (HackFileManager.statusToken is { } cts)
        //{
        //    await cts.CancelAsync();
        //}
        //IsInProcess = false;
    }

	private async void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
	{
        if (IsInProcess)
        {
		    args.Cancel = DialogResult.OK != await MessageBox.ShowAsync(
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
	}
	private StatusDialog(string titleText) : this()
    {
        ParentWindow?.Title = titleText;
    }
    public void ClearStatus()
    {

    }
    public async Task UpdateStatusDialogLoop(CancellationToken token)
    {
        try
        {
            await Task.Run(async () =>
            {
                while (!Canceled)
                {
                    token.ThrowIfCancellationRequested();
					await (SetDownloaded(HackFileManager.Downloaded));
					await (SetTotalDownloaded(HackFileManager.SessionDownloaded));
					await (AddStatusLines(HackFileManager.QueueAsyncStatus));
					await (SetProgressBar(HackFileManager.SkipCounter + HackFileManager.ProcessCounter, HackFileManager.MaxCount));
					await Task.Delay(100, token);
			    }
		    }, token);
        }
        catch
        {
            Debug.WriteLine("Status dialog loop cancelled.");
		}
    }
    public void EndStatusDialogLoop()
    {
        Canceled = true;
    }
	public async Task AddStatusLine(StatusMessage action, string description)
    {
        await AddStatusLine((action, description));
    }
    public async Task AddStatusLines(ConcurrentQueue<(StatusMessage action, string description)> values)
    {
        await AddStatusLinesInternal(values);
    }
    public async Task SetProgressBar(int value, int max)
    {
        await SetProgressBarInternal([value, max]);
    }
    private async Task SetProgressBarInternal(int[] @params)
    {
        await this.DispatcherQueue.ExecuteUIAsync(()=>
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
    private async Task AddStatusLinesInternal(ConcurrentQueue<(StatusMessage action, string description)> values)
    {
		await this.DispatcherQueue.ExecuteUIAsync(()=>
        {
            while (values.TryDequeue(out var value))
            {
				GetDataGrid(value.action, out var collection, out var messageLog);
				
                var lvItem = GridHelp.EmptyListItem<BasicStatusMessage>(messageLog);
				lvItem.Status = value.action;
				lvItem.Message = value.description;
				//ColorizeStatus(item, lvItem);
				// set background color, based on status action

				collection.Insert(0, lvItem);
			}


			RemoveExcessLines(StatusList);
			RemoveExcessLines(InfoList);
            RemoveExcessLines(ErrorList);
		});
    }
	private static void RemoveExcessLines(DataGrid grid)
	{
		var collection = grid.ItemsSource as ObservableCollection<BasicStatusMessage>;
		if (collection == null) return;

		int max = HistoryLength ?? 1000;
		if (collection.Count > max)
		{
			var trimmed = new ObservableCollection<BasicStatusMessage>(
				collection.Take(max)
			);
			grid.ItemsSource = trimmed; // one CollectionChanged event
		}
        //grid.UpdateLayout();
		//grid.ScrollIntoView((grid.ItemsSource as ObservableCollection<BasicStatusMessage>)?.LastOrDefault(), null);
	}
    private async Task AddStatusLine((StatusMessage action, string description) statusMessage)
    {
        await this.DispatcherQueue.ExecuteUIAsync(()=>
        {
            // we are executing in the UI thread
            GetDataGrid(statusMessage.action, out var collection, out var messageLog);
            var lvItem = GridHelp.EmptyListItem<BasicStatusMessage>(messageLog);
            lvItem.Status = statusMessage.action;
            lvItem.Message = statusMessage.description;
            // set background color, based on status action
            collection.Insert(0, lvItem);
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
    private void CmdCancelClick(object sender, RoutedEventArgs arg)
    {
        Canceled = true;
    }
    private void CmdCloseClick(object sender, RoutedEventArgs arg)
    {
		Canceled = true;
		ParentWindow?.Close();
	}
    private void StatusSettings_Click(object sender, RoutedEventArgs arg)
    {
        //var page = InstanceManager.GetAPage<StatusSettings>();
        var window = WindowHelper.CreateWindowPage<ApplicationSettingsPage>();
    }
	public async Task SetDownloaded(long downloadBytes)
	{
        await this.DispatcherQueue.ExecuteUIAsync(() =>
        {
            Downloaded.Text = $"Downloaded: {OperatorConverter.LongBytesToString(downloadBytes)}";
        });
	}
	public async Task SetTotalDownloaded(long sessionDownloadBytes)
	{
        await this.DispatcherQueue.ExecuteUIAsync(() =>
        {
            TotalDownload.Text = $"Session Downloaded: {OperatorConverter.LongBytesToString(sessionDownloadBytes)}";
        });
    }
}
