using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

using HackPDM.Domain.Representation;
using HackPDM.Mvvm.ViewModels.Settings;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Shared.GlobalData;
using HackPDM.Shell;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using Windows.Foundation;
using Windows.UI;

namespace HackPDM.Mvvm.Views.Settings;

/// <summary>The status window: full 3-pane log or compact category bar with hover error/warning popups, bound to <see cref="StatusDialogViewModel"/>.</summary>
public sealed partial class StatusDialogView : Page
{
	private static readonly Color WarningAccent = Color.FromArgb( 255, 224, 168, 46 );
	private static readonly Color WarningTint = Color.FromArgb( 42, 224, 168, 46 );
	private static readonly Color ErrorAccent = Color.FromArgb( 255, 229, 72, 77 );
	private static readonly Color ErrorTint = Color.FromArgb( 42, 229, 72, 77 );

	private bool _allowClose;
	private bool _isClosing;
	private bool _isDocked;
	private bool _isMinimal;
	private Window? _settingsWindow;
	private DispatcherQueueTimer? _hoverCloseTimer;
	private StatusMessage? _activeHoverKind;

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
		SizeChanged += ( _, _ ) => UpdateDensity();
	}

	internal bool IsMinimalMode => _isMinimal;

	internal void SetHostMode( bool isDocked )
	{
		_isDocked = isDocked;
		if( isDocked )
			ParentWindow = null;
		UpdateHostButtons();
		UpdateDensity();
	}

	internal void SetMinimalMode( bool minimal )
	{
		_isMinimal = minimal;
		if( !minimal )
			HideHoverPopup();
		FullStatusRoot.Visibility = minimal ? Visibility.Collapsed : Visibility.Visible;
		MinimalStatusRoot.Visibility = minimal ? Visibility.Visible : Visibility.Collapsed;
		UpdateHostButtons();
		UpdateCategoryPillAccents();
		UpdateDensity();
	}

	private void UpdateHostButtons()
	{
		bool canDock = !_isDocked && HackApp.HomePage is not null;
		DockStatusBtn.Visibility = canDock ? Visibility.Visible : Visibility.Collapsed;
		MiniDockStatusBtn.Visibility = canDock ? Visibility.Visible : Visibility.Collapsed;
		MiniCloseStatusBtn.Visibility = !_isDocked ? Visibility.Visible : Visibility.Collapsed;
	}

	internal Task RequestCloseAsync() => CloseWindowAsync();

	internal void OpenHoverPopupForTest( StatusMessage kind )
	{
		Border pill = kind == StatusMessage.ERROR ? ErrorsPill : WarningsPill;
		ShowHoverPopup( kind, pill );
	}

	internal void ClickHoverPopupForTest()
	{
		HideHoverPopup();
		AppComposition.Dialogs.ShowFullStatusWindow();
	}

	private void UpdateDensity()
	{
		if( _isMinimal )
			return;
		bool compact = ActualHeight > 0 && ActualHeight < 340;
		FullStatusRoot.Margin = compact ? new Thickness( 10, 6, 10, 8 ) : new Thickness( 12 );
		FullStatusRoot.RowSpacing = compact ? 4 : 8;

		bool narrowFooter = ActualWidth > 0 && ActualWidth < 1250;
		StatusFooterGrid.ColumnDefinitions.Clear();
		StatusFooterGrid.RowDefinitions.Clear();
		if( narrowFooter )
		{
			StatusFooterGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 1, GridUnitType.Star ) } );
			StatusFooterGrid.RowDefinitions.Add( new RowDefinition { Height = GridLength.Auto } );
			StatusFooterGrid.RowDefinitions.Add( new RowDefinition { Height = GridLength.Auto } );
			Grid.SetRow( StatusMetricsPanel, 0 );
			Grid.SetColumn( StatusMetricsPanel, 0 );
			Grid.SetRow( StatusActionsPanel, 1 );
			Grid.SetColumn( StatusActionsPanel, 0 );
		}
		else
		{
			StatusFooterGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 1, GridUnitType.Star ) } );
			StatusFooterGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = GridLength.Auto } );
			StatusFooterGrid.RowDefinitions.Add( new RowDefinition { Height = GridLength.Auto } );
			Grid.SetRow( StatusMetricsPanel, 0 );
			Grid.SetColumn( StatusMetricsPanel, 0 );
			Grid.SetRow( StatusActionsPanel, 0 );
			Grid.SetColumn( StatusActionsPanel, 1 );
		}
	}

	private void DockStatusBtn_Click( object sender, RoutedEventArgs e )
		=> AppComposition.Dialogs.DockStatusWindow();

	private void SwitchToMinimalStatus_Click( object sender, RoutedEventArgs e )
		=> AppComposition.Dialogs.SetStatusMinimalMode( true );

	private void OpenFullStatus_Click( object sender, RoutedEventArgs e )
	{
		HideHoverPopup();
		AppComposition.Dialogs.ShowFullStatusWindow();
	}

	private void OnLoaded( object sender, RoutedEventArgs e )
	{
		if( !_isDocked )
			ParentWindow ??= WindowHelper.WindowOf( this );
		ViewModel.OperationCompleted -= OnOperationCompleted;
		ViewModel.OperationCompleted += OnOperationCompleted;
		ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
		ViewModel.PropertyChanged += ViewModel_PropertyChanged;
		UpdateCategoryPillAccents();
		UpdateDensity();
	}

	private void OnUnloaded( object sender, RoutedEventArgs e )
	{
		ViewModel.OperationCompleted -= OnOperationCompleted;
		ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
		HideHoverPopup();
	}

	private void ViewModel_PropertyChanged( object? sender, PropertyChangedEventArgs e )
	{
		if( e.PropertyName is nameof( StatusDialogViewModel.WarningCount ) or nameof( StatusDialogViewModel.ErrorCount ) )
		{
			UpdateCategoryPillAccents();
			if( StatusHoverPopup.IsOpen && _activeHoverKind is { } kind )
			{
				if( ( kind == StatusMessage.WARNING && e.PropertyName == nameof( StatusDialogViewModel.WarningCount ) )
					|| ( kind == StatusMessage.ERROR && e.PropertyName == nameof( StatusDialogViewModel.ErrorCount ) ) )
					ShowHoverPopup( kind, kind == StatusMessage.ERROR ? ErrorsPill : WarningsPill );
			}
		}
	}

	private void UpdateCategoryPillAccents()
	{
		if( ViewModel.WarningCount > 0 )
		{
			WarningsPill.BorderBrush = new SolidColorBrush( WarningAccent );
			WarningsPill.Background = new SolidColorBrush( WarningTint );
			WarningsPillIcon.Foreground = new SolidColorBrush( WarningAccent );
		}
		else
		{
			WarningsPill.ClearValue( Border.BorderBrushProperty );
			WarningsPill.ClearValue( Border.BackgroundProperty );
			WarningsPillIcon.ClearValue( FontIcon.ForegroundProperty );
		}

		if( ViewModel.ErrorCount > 0 )
		{
			ErrorsPill.BorderBrush = new SolidColorBrush( ErrorAccent );
			ErrorsPill.Background = new SolidColorBrush( ErrorTint );
			ErrorsPillIcon.Foreground = new SolidColorBrush( ErrorAccent );
		}
		else
		{
			ErrorsPill.ClearValue( Border.BorderBrushProperty );
			ErrorsPill.ClearValue( Border.BackgroundProperty );
			ErrorsPillIcon.ClearValue( FontIcon.ForegroundProperty );
		}
	}

	private void WarningsPill_PointerEntered( object sender, PointerRoutedEventArgs e )
		=> ShowHoverPopup( StatusMessage.WARNING, WarningsPill );

	private void ErrorsPill_PointerEntered( object sender, PointerRoutedEventArgs e )
		=> ShowHoverPopup( StatusMessage.ERROR, ErrorsPill );

	private void CategoryPill_PointerExited( object sender, PointerRoutedEventArgs e )
		=> ScheduleHoverPopupClose();

	private void CategoryPill_PointerPressed( object sender, PointerRoutedEventArgs e )
	{
		e.Handled = true;
		HideHoverPopup();
		AppComposition.Dialogs.ShowFullStatusWindow();
	}

	private void HoverPopup_PointerEntered( object sender, PointerRoutedEventArgs e )
		=> CancelHoverCloseTimer();

	private void HoverPopup_PointerExited( object sender, PointerRoutedEventArgs e )
		=> ScheduleHoverPopupClose();

	private void HoverPopupCard_PointerPressed( object sender, PointerRoutedEventArgs e )
	{
		e.Handled = true;
		HideHoverPopup();
		AppComposition.Dialogs.ShowFullStatusWindow();
	}

	private void ShowHoverPopup( StatusMessage kind, Border anchorPill )
	{
		if( !_isMinimal || XamlRoot is null )
			return;
		CancelHoverCloseTimer();
		_activeHoverKind = kind;

		bool isError = kind == StatusMessage.ERROR;
		var source = isError ? ViewModel.OErrorsOnly : ViewModel.OWarnings;
		const int maxShown = 8;
		List<BasicStatusMessage> recent = [];
		for( int i = source.Count - 1; i >= 0 && recent.Count < maxShown; i-- )
			recent.Add( source[ i ] );

		HoverPopupIcon.Glyph = isError ? "\uEA39" : "\uE7BA";
		HoverPopupIcon.Foreground = new SolidColorBrush( isError ? ErrorAccent : WarningAccent );
		HoverPopupCard.BorderBrush = new SolidColorBrush( isError ? ErrorAccent : WarningAccent );
		HoverPopupTitle.Text = isError ? $"Errors ({source.Count})" : $"Warnings ({source.Count})";

		HoverPopupItems.ItemsSource = recent;
		HoverPopupItems.Visibility = recent.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
		HoverPopupEmptyText.Text = isError ? "No errors recorded." : "No warnings recorded.";
		HoverPopupEmptyText.Visibility = recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

		int hidden = source.Count - recent.Count;
		HoverPopupOverflowText.Text = hidden > 0 ? $"+{hidden} more in full Status Log…" : string.Empty;
		HoverPopupOverflowText.Visibility = hidden > 0 ? Visibility.Visible : Visibility.Collapsed;

		StatusHoverPopup.IsOpen = false;
		if( _isDocked && HackApp.HomePage is { } home )
		{
			StatusHoverPopup.Child = null;
			HoverPopupCard.Measure( new Size( 440, 340 ) );
			double cardH = Math.Clamp( HoverPopupCard.DesiredSize.Height, 110, 340 );
			home.ShowOverlay( HoverPopupCard, anchorPill, 440, cardH );
			return;
		}

		HackApp.HomePage?.HideOverlay( HoverPopupCard );
		if( StatusHoverPopup.Child is null )
			StatusHoverPopup.Child = HoverPopupCard;

		StatusHoverPopup.XamlRoot = XamlRoot;
		HoverPopupCard.Measure( new Size( 440, 340 ) );
		double popupW = 440;
		double popupH = Math.Clamp( HoverPopupCard.DesiredSize.Height, 110, 340 );

		Point pillInGrid = anchorPill.TransformToVisual( StatusRootGrid ).TransformPoint( new Point( 0, 0 ) );
		Point pillInWindow = XamlRoot.Content is UIElement rootContent
			? anchorPill.TransformToVisual( rootContent ).TransformPoint( new Point( 0, 0 ) )
			: pillInGrid;

		double rootW = XamlRoot.Size.Width > 0 ? XamlRoot.Size.Width : ActualWidth;
		double xOffset = Math.Clamp( pillInGrid.X, 8, Math.Max( 8, rootW - popupW - 24 ) );
		double yOffset = pillInWindow.Y >= popupH + 14
			? pillInGrid.Y - popupH - 6
			: pillInGrid.Y + anchorPill.ActualHeight + 6;

		StatusHoverPopup.HorizontalOffset = xOffset;
		StatusHoverPopup.VerticalOffset = yOffset;
		StatusHoverPopup.IsOpen = true;
	}

	private void ScheduleHoverPopupClose()
	{
		if( DispatcherQueue is null )
		{
			HideHoverPopup();
			return;
		}
		_hoverCloseTimer ??= CreateHoverCloseTimer();
		_hoverCloseTimer.Stop();
		_hoverCloseTimer.Start();
	}

	private DispatcherQueueTimer CreateHoverCloseTimer()
	{
		var timer = DispatcherQueue.CreateTimer();
		timer.Interval = TimeSpan.FromMilliseconds( 240 );
		timer.IsRepeating = false;
		timer.Tick += ( _, _ ) => HideHoverPopup();
		return timer;
	}

	private void CancelHoverCloseTimer() => _hoverCloseTimer?.Stop();

	private void HideHoverPopup()
	{
		CancelHoverCloseTimer();
		_activeHoverKind = null;
		StatusHoverPopup.IsOpen = false;
		HackApp.HomePage?.HideOverlay( HoverPopupCard );
	}

	private void OnOperationCompleted()
	{
		if( ViewModel.AutoClose )
			CloseImmediately();
	}

	private void CloseImmediately()
	{
		_allowClose = true;
		HideHoverPopup();
		if( !_isDocked && ( ParentWindow ?? WindowHelper.WindowOf( this ) ) is { } win )
			win.Close();
		else
			AppComposition.Dialogs.CloseStatusWindow();
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

	private void StatusHorizontalSplitter_DragDelta( double delta )
	{
		double totalH = OperationsRow.ActualHeight + LowerLogsRow.ActualHeight;
		if( totalH <= 180 )
			return;
		double top = Math.Clamp( OperationsRow.ActualHeight + delta, 100, totalH - 100 );
		OperationsRow.Height = new GridLength( top, GridUnitType.Star );
		LowerLogsRow.Height = new GridLength( Math.Max( 100, totalH - top ), GridUnitType.Star );
	}

	private void StatusVerticalSplitter_DragDelta( double delta )
	{
		double totalW = InfoLogCol.ActualWidth + ErrorLogCol.ActualWidth;
		if( totalW <= 260 )
			return;
		double left = Math.Clamp( InfoLogCol.ActualWidth + delta, 160, totalW - 160 );
		InfoLogCol.Width = new GridLength( left, GridUnitType.Star );
		ErrorLogCol.Width = new GridLength( Math.Max( 160, totalW - left ), GridUnitType.Star );
	}

	private void CopyDiagnosticsBtn_Click( object sender, RoutedEventArgs e )
	{
		string summary = ViewModel.BuildDiagnosticSummary();
		try
		{
			var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
			package.SetText( summary );
			Windows.ApplicationModel.DataTransfer.Clipboard.SetContent( package );
			ViewModel.Session.Log( StatusMessage.INFO, "Copied diagnostic summary to clipboard." );
		}
		catch( Exception ex )
		{
			HackPDM.Core.Logging.HackLogger.Warn( "StatusDialog", "Failed to copy diagnostic summary to clipboard", ex );
		}
	}
}
