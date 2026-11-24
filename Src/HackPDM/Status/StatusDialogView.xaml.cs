using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;
using HackPDM.Settings;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using Windows.Foundation;
using Windows.UI;

namespace HackPDM.Status;

/// <summary>The status window: full 3-pane log or compact category bar with hover error/warning popups, bound to <see cref="StatusDialogViewModel"/>.</summary>
public sealed partial class StatusDialogView : Page, IDockablePanel
{
	private static readonly SolidColorBrush InfoAccentBrush = new( Color.FromArgb( 255, 76, 160, 232 ) );
	private static readonly SolidColorBrush WarningAccentBrush = new( Color.FromArgb( 255, 224, 168, 46 ) );
	private static readonly SolidColorBrush WarningTintBrush = new( Color.FromArgb( 42, 224, 168, 46 ) );
	private static readonly SolidColorBrush ErrorAccentBrush = new( Color.FromArgb( 255, 229, 72, 77 ) );
	private static readonly SolidColorBrush ErrorTintBrush = new( Color.FromArgb( 42, 229, 72, 77 ) );

	private bool _allowClose;
	private bool _isClosing;
	private bool _isDocked;
	private bool _isMinimal;
	private bool? _lastCompactDensity;
	private bool? _lastNarrowFooter;
	private bool? _lastWarningsActive;
	private bool? _lastErrorsActive;
	private Window? _settingsWindow;
	private DispatcherQueueTimer? _hoverCloseTimer;
	private StatusMessage? _activeHoverKind;

	// Strongly held hover popup card & children (avoids Uno Popup ElementNameSubject weak-ref nulls when detached)
	private Border? _hoverCard;
	private FontIcon? _hoverIcon;
	private TextBlock? _hoverTitle;
	private TextBlock? _hoverOpenLink;
	private ItemsControl? _hoverItems;
	private TextBlock? _hoverEmptyText;
	private TextBlock? _hoverOverflowText;
	private TextBlock? _hoverHintText;
	private readonly RangeObservableCollection<BasicStatusMessage> _hoverMessages = [];
	private readonly List<BasicStatusMessage> _hoverScratch = new( 8 );

	public StatusDialogViewModel ViewModel => AppComposition.StatusLog;

	/// <summary>The window showing this page; closing it while work runs asks for confirmation.</summary>
	public Window? HostWindow
	{
		get => ParentWindow;
		set => ParentWindow = value;
	}

	public Window? ParentWindow
	{
		get;
		set
		{
			if( ReferenceEquals( field, value ) )
				return;
			if( field is not null )
			{
				try { field.AppWindow.Closing -= AppWindow_Closing; }
				catch { }
			}
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

#if SMOKE_TESTS
	internal bool IsMinimalMode => _isMinimal;
#endif

	public void SetHostMode( bool isDocked )
	{
		_isDocked = isDocked;
		HideHoverPopup();
		if( isDocked )
			ParentWindow = null;
		UpdateHostButtons();
		UpdateDensity();
	}

	public void SetMinimalMode( bool minimal )
	{
		_isMinimal = minimal;
		if( !minimal )
			HideHoverPopup();
		FullStatusRoot.Visibility = ( !minimal ).ToVisibility();
		MinimalStatusRoot.Visibility = minimal.ToVisibility();
		UpdateHostButtons();
		UpdateCategoryPillAccents();
		UpdateDensity();
	}

	private void UpdateHostButtons()
	{
		bool canDock = !_isDocked && HackApp.HomePage is not null;
		DockStatusBtn.Visibility = canDock.ToVisibility();
		MiniDockStatusBtn.Visibility = canDock.ToVisibility();
		MiniCloseStatusBtn.Visibility = ( !_isDocked ).ToVisibility();
	}

	internal Task RequestCloseAsync() => CloseWindowAsync();

#if SMOKE_TESTS
	internal void OpenHoverPopupForTest( StatusMessage kind )
	{
		Border pill = kind switch
		{
			StatusMessage.ERROR => ErrorsPill,
			StatusMessage.INFO => InfoPill,
			_ => WarningsPill,
		};
		ShowHoverPopup( kind, pill );
	}

	internal void ClickHoverPopupForTest()
	{
		HideHoverPopup();
		AppComposition.Dialogs.ShowFullStatusWindow();
	}
#endif

	private void UpdateDensity()
	{
		if( _isMinimal )
			return;
		bool compact = ActualHeight > 0 && ActualHeight < 340;
		if( _lastCompactDensity != compact )
		{
			_lastCompactDensity = compact;
			FullStatusRoot.Margin = compact ? new Thickness( 10, 6, 10, 8 ) : new Thickness( 12 );
			FullStatusRoot.RowSpacing = compact ? 4 : 8;
		}

		bool narrowFooter = ActualWidth > 0 && ActualWidth < 1250;
		if( _lastNarrowFooter == narrowFooter )
			return;
		_lastNarrowFooter = narrowFooter;
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
		if( _isDocked )
			ParentWindow = null;
		else
			ParentWindow ??= WindowHelper.WindowOf( this );
		ViewModel.OperationCompleted -= OnOperationCompleted;
		ViewModel.OperationCompleted += OnOperationCompleted;
		ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
		ViewModel.PropertyChanged += ViewModel_PropertyChanged;
		AppComposition.Theme.ThemeChanged -= OnThemeChanged;
		AppComposition.Theme.ThemeChanged += OnThemeChanged;
		UpdateCategoryPillAccents();
		UpdateDensity();
	}

	private void OnUnloaded( object sender, RoutedEventArgs e )
	{
		ViewModel.OperationCompleted -= OnOperationCompleted;
		ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
		AppComposition.Theme.ThemeChanged -= OnThemeChanged;
		if( _settingsWindow is { } settingsWindow )
		{
			settingsWindow.Closed -= OnSettingsWindowClosed;
			_settingsWindow = null;
		}
		HideHoverPopup();
	}

	private void OnThemeChanged()
	{
		_lastWarningsActive = null;
		_lastErrorsActive = null;
		UpdateCategoryPillAccents();
		RefreshHoverCardBrushes();
	}

	private void ViewModel_PropertyChanged( object? sender, PropertyChangedEventArgs e )
	{
		if( e.PropertyName is nameof( StatusDialogViewModel.WarningCount )
			or nameof( StatusDialogViewModel.ErrorCount )
			or nameof( StatusDialogViewModel.InfoCount ) )
		{
			UpdateCategoryPillAccents();
			if( _isMinimal && _activeHoverKind is { } kind && IsHoverPopupCurrentlyOpen() )
			{
				if( ( kind == StatusMessage.WARNING && e.PropertyName == nameof( StatusDialogViewModel.WarningCount ) )
					|| ( kind == StatusMessage.ERROR && e.PropertyName == nameof( StatusDialogViewModel.ErrorCount ) )
					|| ( kind == StatusMessage.INFO && e.PropertyName == nameof( StatusDialogViewModel.InfoCount ) ) )
				{
					Border pill = kind switch
					{
						StatusMessage.ERROR => ErrorsPill,
						StatusMessage.INFO => InfoPill,
						_ => WarningsPill,
					};
					ShowHoverPopup( kind, pill );
				}
			}
		}
	}

	private bool IsHoverPopupCurrentlyOpen()
		=> _isDocked ? _hoverCard?.Parent is not null : StatusHoverPopup.IsOpen;

	private void UpdateCategoryPillAccents()
	{
		bool warningsActive = ViewModel.WarningCount > 0;
		if( _lastWarningsActive != warningsActive )
		{
			_lastWarningsActive = warningsActive;
			ApplyPillAccent( WarningsPill, WarningsPillIcon, warningsActive, WarningAccentBrush, WarningTintBrush );
		}
		bool errorsActive = ViewModel.ErrorCount > 0;
		if( _lastErrorsActive != errorsActive )
		{
			_lastErrorsActive = errorsActive;
			ApplyPillAccent( ErrorsPill, ErrorsPillIcon, errorsActive, ErrorAccentBrush, ErrorTintBrush );
		}
	}

	private static void ApplyPillAccent( Border pill, FontIcon icon, bool active, Brush accent, Brush tint )
	{
		if( active )
		{
			pill.BorderBrush = accent;
			pill.Background = tint;
			icon.Foreground = accent;
			return;
		}
		pill.BorderBrush = ThemeResources.TryGetBrush<Brush>( "CardStrokeColorDefaultBrush" );
		pill.Background = ThemeResources.TryGetBrush<Brush>( "SubtleFillColorSecondaryBrush" );
		icon.ClearValue( FontIcon.ForegroundProperty );
	}

	private void InfoPill_PointerEntered( object sender, PointerRoutedEventArgs e ) => ShowHoverPopup( StatusMessage.INFO, InfoPill );
	private void WarningsPill_PointerEntered( object sender, PointerRoutedEventArgs e ) => ShowHoverPopup( StatusMessage.WARNING, WarningsPill );
	private void ErrorsPill_PointerEntered( object sender, PointerRoutedEventArgs e ) => ShowHoverPopup( StatusMessage.ERROR, ErrorsPill );
	private void CategoryPill_PointerExited( object sender, PointerRoutedEventArgs e ) => ScheduleHoverPopupClose();
	private void HoverPopup_PointerEntered( object sender, PointerRoutedEventArgs e ) => CancelHoverCloseTimer();
	private void HoverPopup_PointerExited( object sender, PointerRoutedEventArgs e ) => ScheduleHoverPopupClose();

	private void CategoryPill_PointerPressed( object sender, PointerRoutedEventArgs e ) => OpenFullFromPointer( e );
	private void HoverPopupCard_PointerPressed( object sender, PointerRoutedEventArgs e ) => OpenFullFromPointer( e );

	private void OpenFullFromPointer( PointerRoutedEventArgs e )
	{
		e.Handled = true;
		HideHoverPopup();
		AppComposition.Dialogs.ShowFullStatusWindow();
	}

	private Border EnsureHoverCard()
	{
		if( _hoverCard is not null )
			return _hoverCard;

		_hoverIcon = new FontIcon { FontSize = 14 };
		_hoverTitle = new TextBlock { FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
		StackPanel titleStack = new() { VerticalAlignment = VerticalAlignment.Center, Orientation = Orientation.Horizontal, Spacing = 6 };
		titleStack.Children.Add( _hoverIcon );
		titleStack.Children.Add( _hoverTitle );

		_hoverOpenLink = new TextBlock
		{
			VerticalAlignment = VerticalAlignment.Center,
			FontSize = 11,
			Text = "Open Full Status →",
		};

		Grid headerGrid = new() { ColumnSpacing = 8 };
		headerGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 1, GridUnitType.Star ) } );
		headerGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = GridLength.Auto } );
		titleStack.SetGrid( 0, 0 );
		_hoverOpenLink.SetGrid( 0, 1 );
		headerGrid.Children.Add( titleStack );
		headerGrid.Children.Add( _hoverOpenLink );

		_hoverItems = new ItemsControl
		{
			ItemsSource = _hoverMessages,
			ItemTemplate = StatusRootGrid.Resources.TryGetValue( "HoverPopupItemTemplate", out object? template ) ? template as DataTemplate : null,
		};
		_hoverEmptyText = new TextBlock { FontSize = 12, Visibility = Visibility.Collapsed };
		_hoverOverflowText = new TextBlock { FontSize = 11, Visibility = Visibility.Collapsed };
		_hoverHintText = new TextBlock { FontSize = 11, Text = "Click this popup to open the full Status Dialog." };

		StackPanel rootStack = new() { Spacing = 8 };
		rootStack.Children.Add( headerGrid );
		rootStack.Children.Add( new ScrollViewer { MaxHeight = 220, Content = _hoverItems } );
		rootStack.Children.Add( _hoverEmptyText );
		rootStack.Children.Add( _hoverOverflowText );
		rootStack.Children.Add( _hoverHintText );

		_hoverCard = new Border
		{
			Width = 440,
			Padding = new Thickness( 12, 10, 12, 10 ),
			BorderThickness = new Thickness( 1.5 ),
			CornerRadius = new CornerRadius( 8 ),
			Child = rootStack,
		};
		_hoverCard.PointerEntered += HoverPopup_PointerEntered;
		_hoverCard.PointerExited += HoverPopup_PointerExited;
		_hoverCard.PointerPressed += HoverPopupCard_PointerPressed;
		RefreshHoverCardBrushes();
		return _hoverCard;
	}

	private void RefreshHoverCardBrushes()
	{
		if( _hoverCard is null )
			return;
		_hoverCard.Background = ThemeResources.TryGetBrush<Brush>( "ApplicationPageBackgroundThemeBrush" );
		if( _hoverOpenLink is not null )
			_hoverOpenLink.Foreground = ThemeResources.TryGetBrush<Brush>( "AccentTextFillColorPrimaryBrush" );
		Brush? secondary = ThemeResources.TryGetBrush<Brush>( "TextFillColorSecondaryBrush" );
		if( _hoverEmptyText is not null )
			_hoverEmptyText.Foreground = secondary;
		if( _hoverOverflowText is not null )
			_hoverOverflowText.Foreground = secondary;
		if( _hoverHintText is not null )
			_hoverHintText.Foreground = ThemeResources.TryGetBrush<Brush>( "TextFillColorTertiaryBrush" );
	}

	private void ShowHoverPopup( StatusMessage kind, Border anchorPill )
	{
		if( !_isMinimal || XamlRoot is null )
			return;
		CancelHoverCloseTimer();
		bool alreadyOpenSameKind = _activeHoverKind == kind && IsHoverPopupCurrentlyOpen();
		_activeHoverKind = kind;

		Border card = EnsureHoverCard();
		var (source, glyph, accentBrush, titlePrefix, emptyMessage) = kind switch
		{
			StatusMessage.ERROR => ( ViewModel.OErrorsOnly, "\uEA39", ErrorAccentBrush, "Errors", "No errors recorded." ),
			StatusMessage.INFO => ( ViewModel.OInfo, "\uE946", InfoAccentBrush, "Info", "No info messages recorded." ),
			_ => ( ViewModel.OWarnings, "\uE7BA", WarningAccentBrush, "Warnings", "No warnings recorded." ),
		};

		const int maxShown = 8;
		int take = Math.Min( source.Count, maxShown );
		if( HasHoverMessagesChanged( source, take ) )
		{
			_hoverScratch.Clear();
			for( int i = 0; i < take; i++ )
				_hoverScratch.Add( source[ i ] );
			_hoverMessages.ReplaceAll( _hoverScratch );
		}

		if( !alreadyOpenSameKind )
		{
			_hoverIcon!.Glyph = glyph;
			_hoverIcon.Foreground = accentBrush;
			card.BorderBrush = accentBrush;
			_hoverEmptyText!.Text = emptyMessage;
		}
		_hoverTitle!.Text = $"{titlePrefix} ({source.Count})";

		Visibility itemsVis = take > 0 ? Visibility.Visible : Visibility.Collapsed;
		if( _hoverItems!.Visibility != itemsVis )
			_hoverItems.Visibility = itemsVis;

		Visibility emptyVis = take == 0 ? Visibility.Visible : Visibility.Collapsed;
		if( _hoverEmptyText!.Visibility != emptyVis )
			_hoverEmptyText.Visibility = emptyVis;

		int hidden = source.Count - take;
		_hoverOverflowText!.Text = hidden > 0 ? $"+{hidden} more in full Status Log…" : string.Empty;
		Visibility overflowVis = hidden > 0 ? Visibility.Visible : Visibility.Collapsed;
		if( _hoverOverflowText.Visibility != overflowVis )
			_hoverOverflowText.Visibility = overflowVis;

		if( alreadyOpenSameKind )
			return;

		StatusHoverPopup.IsOpen = false;
		if( _isDocked && HackApp.HomePage is { } home )
		{
			StatusHoverPopup.Child = null;
			card.Measure( new Size( 440, 340 ) );
			double cardH = Math.Clamp( card.DesiredSize.Height, 110, 340 );
			home.ShowOverlay( card, anchorPill, 440, cardH );
			return;
		}

		HackApp.HomePage?.HideOverlay( card );
		if( !ReferenceEquals( StatusHoverPopup.Child, card ) )
			StatusHoverPopup.Child = card;

		StatusHoverPopup.XamlRoot = XamlRoot;
#if WINDOWS
		StatusHoverPopup.ShouldConstrainToRootBounds = false;
#endif
		card.Measure( new Size( 440, 340 ) );
		double popupW = 440;
		double popupH = Math.Clamp( card.DesiredSize.Height, 110, 340 );

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

	private bool HasHoverMessagesChanged( ObservableRingBuffer<BasicStatusMessage> source, int take )
	{
		if( _hoverMessages.Count != take )
			return true;
		for( int i = 0; i < take; i++ )
		{
			if( !ReferenceEquals( _hoverMessages[ i ], source[ i ] ) )
				return true;
		}
		return false;
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
		StatusHoverPopup.Child = null;
		HackApp.HomePage?.HideOverlay( _hoverCard );
	}

	private void OnOperationCompleted()
	{
		if( ViewModel.AutoClose && ViewModel.ErrorCount == 0 && ViewModel.WarningCount == 0 )
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
		catch( Exception ex )
		{
			HackLogger.Warn( "StatusDialog", $"Closing status window failed: {ex.Message}", ex );
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
		page.SelectSection( ApplicationSettingsView.StatusSectionKey );
		window.Title = "Status Settings";
		_settingsWindow = window;
		window.Closed += OnSettingsWindowClosed;
	}

	private void OnSettingsWindowClosed( object sender, WindowEventArgs args )
	{
		if( sender is Window win )
			win.Closed -= OnSettingsWindowClosed;
		_settingsWindow = null;
	}

	private async void AppWindow_Closing( AppWindow sender, AppWindowClosingEventArgs args )
	{
		try
		{
			if( _allowClose || _isDocked || ParentWindow is not { } parent || !ReferenceEquals( parent.AppWindow, sender ) )
				return;
			object? hosted = ( parent.Content as Frame )?.Content ?? parent.Content;
			if( !ReferenceEquals( hosted, this ) || !ViewModel.IsInProcess )
				return;
			// hold the close (it can't wait for the answer), then close for real if the user confirms
			args.Cancel = true;
			await CloseWindowAsync();
		}
		catch( Exception ex )
		{
			HackLogger.Warn( "StatusDialog", $"AppWindow closing handler failed: {ex.Message}", ex );
		}
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
		if( ClipboardHelper.TrySetText( summary, "StatusDialog" ) )
			ViewModel.AddLog( new BasicStatusMessage { Status = StatusMessage.INFO, Message = "Copied diagnostic summary to clipboard." } );
	}
}
