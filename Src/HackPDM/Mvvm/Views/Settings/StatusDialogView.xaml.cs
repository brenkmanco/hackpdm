using System.Threading.Tasks;

using HackPDM.Mvvm.ViewModels.Settings;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Shell;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Mvvm.Views.Settings;

/// <summary>The status window: the log and progress of the running operation, all bound to <see cref="StatusDialogViewModel"/>.</summary>
public sealed partial class StatusDialogView : Page
{
	private bool _allowClose;
	private bool _isClosing;
	private Window? _settingsWindow;

	public StatusDialogViewModel ViewModel => AppComposition.StatusLog;

	/// <summary>The window showing this page; closing it while work runs asks for confirmation.</summary>
	public Window? ParentWindow
	{
		get;
		set
		{
			if( ReferenceEquals( field, value ) )
				return;
			if( field is not null )
				field.AppWindow.Closing -= AppWindow_Closing;
			field = value;
			if( field is not null )
			{
				_allowClose = false;
				field.AppWindow.Closing += AppWindow_Closing;
			}
		}
	}

	public StatusDialogView()
	{
		InitializeComponent();
		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
	}

	private void OnLoaded( object sender, RoutedEventArgs e )
	{
		ParentWindow ??= WindowHelper.WindowOf( this );
		ViewModel.OperationCompleted -= OnOperationCompleted;
		ViewModel.OperationCompleted += OnOperationCompleted;
	}

	private void OnUnloaded( object sender, RoutedEventArgs e )
	{
		ViewModel.OperationCompleted -= OnOperationCompleted;
	}

	private void OnOperationCompleted()
	{
		if( ViewModel.AutoClose )
			CloseImmediately();
	}

	private void CloseImmediately()
	{
		_allowClose = true;
		( ParentWindow ?? WindowHelper.WindowOf( this ) )?.Close();
	}

	private async Task CloseWindowAsync()
	{
		if( _isClosing )
			return;
		_isClosing = true;
		try
		{
			if( !await ViewModel.ConfirmCloseAsync() )
				return;
			CloseImmediately();
		}
		finally
		{
			_isClosing = false;
		}
	}

	private async void CmdCloseClick( object sender, RoutedEventArgs arg )
		=> await CloseWindowAsync();

	private void StatusSettings_Click( object sender, RoutedEventArgs arg )
	{
		if( _settingsWindow is { } open )
		{
			open.Activate();
			return;
		}
		WindowHelper.CreateWindowAndPage<ApplicationSettingsView>( out var page, out var window );
		page.SelectSection( "Status" );
		window.Title = "Status Settings";
		_settingsWindow = window;
		window.Closed += ( _, _ ) => _settingsWindow = null;
	}

	private async void AppWindow_Closing( AppWindow sender, AppWindowClosingEventArgs args )
	{
		if( _allowClose || !ViewModel.IsInProcess )
			return;
		// hold the close (it can't wait for the answer), then close for real if the user confirms
		args.Cancel = true;
		await CloseWindowAsync();
	}
}
