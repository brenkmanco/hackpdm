using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HackPDM.Abstractions;
using HackPDM.Abstractions.Structures;
using HackPDM.Core;
using HackPDM.Core.Hack;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo.FormTransport;
using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Mvvm.Views.Common.Settings;
using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.Views.Dialogs;
using HackPDM.Mvvm.Views.Common.Helpers;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HackPDM.Shell;

using Brush = Microsoft.UI.Xaml.Media.Brush;
using TableView = WinUI.TableView.TableView;
using ListViewItem = Microsoft.UI.Xaml.Controls.ListViewItem;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM.Mvvm.Views.Settings;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class StatusDialogView : Page
{
    public StatusDialog_VM ViewModel => InstanceManager.Dialog;

    public Window? ParentWindow { get; set; }

    public ObservableRingBuffer<BasicStatusMessage> OStatus => ViewModel.OStatus;
    public ObservableRingBuffer<BasicStatusMessage> OInfo => ViewModel.OInfo;
    public ObservableRingBuffer<BasicStatusMessage> OError => ViewModel.OError;

    public static Brush ColorProcessing { get; set; } = UIStorage.BrushDarkBlue.Value;
    public static Brush ColorSkip { get; set; } = UIStorage.BrushDarkGray.Value;
    public static Brush ColorFound { get; set; } = UIStorage.BrushDarkGray.Value;
    public static Brush ColorSuccess { get; set; } = UIStorage.BrushDarkOliveGreen.Value;
    public static Brush ColorWarning { get; set; } = UIStorage.BrushMustardYellow.Value;
    public static Brush ColorError { get; set; } = UIStorage.BrushDarkRed.Value;
    public static Brush ColorDefaultFore { get; set; } = UIStorage.BrushBlack.Value;
    public static Brush ColorDefaultBack { get; set; } = UIStorage.BrushWhite.Value;

	public bool Canceled { get => ViewModel.Canceled; private set => ViewModel.Canceled = value; }
	public bool HasLoaded { get => ViewModel.HasLoaded; set => ViewModel.HasLoaded = value; }
	internal bool IsInProcess { get => ViewModel.IsInProcess; set => ViewModel.IsInProcess = value; }

	public bool ShowStatusDialog(string titleText)
    {
        //var dlg = new StatusDialog(TitleText);

        ParentWindow ??= WindowHelper.CreateWindowPage<StatusDialogView>();
        return this.Canceled;
    }
    public async Task<bool> ShowWait(string titleText)
    {
        return await AsyncHelper.WaitUntil(() => ParentWindow?.Visible ?? true, 100, 10000);
    }

    public StatusDialogView()
    {
        //HackFileManager.QueueAsyncStatus = new();
        InitializeComponent();
		ClearStatus();
        this.Loaded += new((s, e)=> HasLoaded = true);
		ParentWindow?.AppWindow.Closing += AppWindow_Closing;
		this.Unloaded += StatusDialog_Unloaded;
    }

	private StatusDialogView(string titleText) : this()
    {
        ParentWindow?.Title = titleText;
    }

	

    public static void ClearStatus()
    {

    }
    public void EndStatusDialogLoop()
    {
        Canceled = true;
    }
    public async Task UpdateStatusDialogLoop(CancellationToken token)
    {;
        try
        {
            await Task.Run(async () =>
            {
                while (!Canceled)
                {
                    token.ThrowIfCancellationRequested();
					// only render: the counters are written by IOperationReporter
					await SetDownloadedInternal();
					await SetTotalDownloadedInternal();
					await SetProgressBarInternal();
					await Task.Delay(100, token);
			    }
		    }, token);
        }
        catch
        {
            Debug.WriteLine("Status dialog loop cancelled.");
		}
    }
    
	

	internal async Task SetTotalDownloadedInternal()
	{
        await this.DispatcherQueue.ExecuteUIAsync(() =>
        {
            TotalDownload.Text = $"Session Downloaded: {OperatorConverter.LongBytesToString(StatusDialog_VM.SessionDownloaded)}";
        });
    }
	internal async Task SetDownloadedInternal()
	{
		await this.DispatcherQueue.ExecuteUIAsync( ()
			=> Downloaded.Text = $"Downloaded: {OperatorConverter.LongBytesToString( ViewModel.Downloaded )}" );
	}
    internal async Task SetProgressBarInternal()
    {
        await this.DispatcherQueue.ExecuteUIAsync(()=>
        {
            fileCheckStatus.Maximum = ViewModel.MaxCount;
            fileCheckStatus.Value = ViewModel.TotalProcessed;
            float percent = ViewModel.MaxCount > 0 ? ViewModel.TotalProcessed / (float)ViewModel.MaxCount * 100 : 0;
            ProgressText.Text = $"({percent:f2}%)\n{ViewModel.TotalProcessed} / {ViewModel.MaxCount}";
            SkippedLabel.Text = $"({ViewModel.SkipCounter}) Skipped";
        });
    }
    
	internal async Task AddStatusLineInternal((StatusMessage action, string description) statusMessage)
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

    internal TableView GetList(StatusMessage action) => action switch
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
    internal void ColorizeStatus((StatusMessage action, string description) values, ListViewItem item)
    {
        switch (values.action)
        {
            case StatusMessage.PROCESSING: item.Foreground = ColorProcessing; break;
            case StatusMessage.SKIP: item.Foreground = ColorSkip; break;
            case StatusMessage.FOUND: item.Foreground = ColorFound; break;
            case StatusMessage.SUCCESS: item.Foreground = ColorSuccess; break;
            case StatusMessage.WARNING: item.Background = ColorWarning; break;
            case StatusMessage.ERROR: item.Background = ColorError; ViewModel.ErrorCount++; break;
            default: break;
        }
    }
    internal void GetDataGrid(StatusMessage action, out ObservableRingBuffer<BasicStatusMessage> collection, out TableView? dataGrid)
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
        var window = WindowHelper.CreateWindowPage<ApplicationSettingsView>();
    }
	private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
	{
        if (IsInProcess)
        {
		    args.Cancel = DialogResult.OK != await MessageBox.ShowAsync(
			    "There are items still processing..\nWould you like to continue and close the window and operations?", 
			    "Cancel Operation?",
			    MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
		    // closing the window stops the running operation
		    if (!args.Cancel)
			    HackPDM.Shell.AppComposition.Reporter.Cancel();
        }
	}
	private async void StatusDialog_Unloaded( object sender, RoutedEventArgs e )
	{
		//if (HackFileManager.statusToken is { } cts)
		//{
		//    await cts.CancelAsync();
		//}
		//IsInProcess = false;
	}
}
