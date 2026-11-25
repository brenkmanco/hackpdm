using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading.Tasks;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Pdm;
using HackPDM.FileManager;
using HackPDM.Graph;
using HackPDM.Login;
using HackPDM.Search;
using HackPDM.Settings;
using HackPDM.Status;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;

using Windows.System;

namespace HackPDM.Shell;

/// <summary>
/// The main window's page: a navigation menu and the frame showing the chosen page. Each menu item's
/// Tag is its <see cref="NavigatePageMenu"/> name; <see cref="NavigationService"/> selects items from
/// view models.
/// </summary>
public sealed partial class HomeView : Page
{
	private const int MinimalSearchMinWidth = 960;
	private const int FullSearchMinWidth = 1120;
	private const int FullStatusMinHeight = 720;

	/// <summary>Each page and the window size, title, and menu order it uses.</summary>
	private static readonly FrozenDictionary<NavigatePageMenu, (Type Page, string Config, int Order)> Pages = new Dictionary<NavigatePageMenu, (Type Page, string Config, int Order)>
	{
		[ NavigatePageMenu.HackFileManager ] = ( typeof( HackFileManagerView ), WindowHelper.Presets.HackFileManager, 0 ),
		[ NavigatePageMenu.DependencyGraph ] = ( typeof( DependencyGraphView ), WindowHelper.Presets.DependencyGraph, 1 ),
		[ NavigatePageMenu.ProfileManager ] = ( typeof( ProfileManagerView ), WindowHelper.Presets.ProfileManager, 2 ),
		[ NavigatePageMenu.Configuration ] = ( typeof( ConfigurationView ), WindowHelper.Presets.ConfigSettings, 3 ),
		[ NavigatePageMenu.Settings ] = ( typeof( ApplicationSettingsView ), WindowHelper.Presets.AppSettings, 4 ),
	}.ToFrozenDictionary();

	private NavigatePageMenu _currentMenu = NavigatePageMenu.ProfileManager;
	private ColumnDefinition? _searchColumn;

	public HomeView()
	{
		InitializeComponent();
		AppComposition.Navigation.Attach( this );
		Select( NavigatePageMenu.ProfileManager );
		NavFrame.Navigated += ( _, _ ) => RailBackButton.IsEnabled = NavFrame.CanGoBack;
		Unloaded += HomeView_Unloaded;
		ArrangeSearchDrawer();
	}

	private void HomeView_Unloaded( object sender, RoutedEventArgs e )
	{
		HideOverlay( _overlayElement );
		ShellOverlayCanvas.Children.Clear();
	}

	/// <summary>The frame that hosts the page selected in the navigation menu.</summary>
	internal Frame ContentFrame => NavFrame;

	internal StatusDialogView? StatusPage => DockedStatusHost.Content as StatusDialogView;
	internal SearchOdooView? SearchPage => DockedSearchHost.Content as SearchOdooView;
	internal bool IsStatusDrawerOpen => DockedStatusDrawer.Visibility == Visibility.Visible;
	internal bool IsSearchDrawerOpen => DockedSearchDrawer.Visibility == Visibility.Visible;

	/// <summary>Selects the page's rail item and shows the page.</summary>
	internal void Select( NavigatePageMenu page )
	{
		// Re-selecting a locked tab (e.g. NotLoggedInView) after signing in must still show the real page, so this
		// always shows it; Navigate skips a page that is already open.
		SetRailSelection( Pages.ContainsKey( page ) ? page : NavigatePageMenu.ProfileManager );
		Show( Pages.ContainsKey( page ) ? page : NavigatePageMenu.ProfileManager );
	}

	/// <summary>Fills the indicator pill of the rail item for <paramref name="page"/> (none for null).</summary>
	private void SetRailSelection( NavigatePageMenu? page )
	{
		foreach( NavRailItem item in (NavRailItem[])[ FilesRailItem, GraphRailItem, ProfilesRailItem, ConfigsRailItem, SettingsRailItem ] )
			item.IsSelected = page is { } p && string.Equals( item.Tag as string, p.ToString(), StringComparison.Ordinal );
	}

	private void RailItem_Click( object sender, RoutedEventArgs e )
	{
		if( sender is NavRailItem { Tag: string tag } && Enum.TryParse( tag, out NavigatePageMenu page ) )
			Select( page );
	}

	private void SearchRail_Click( object sender, RoutedEventArgs e ) => AppComposition.Dialogs.ToggleSearchWindow();
	private void StatusRail_Click( object sender, RoutedEventArgs e ) => AppComposition.Dialogs.ToggleStatusWindow();
	private void RailBack_Click( object sender, RoutedEventArgs e ) => GoBack();

	private void Show( NavigatePageMenu page )
	{
		if( AppComposition.Session.IsVersionMismatch && page is not NavigatePageMenu.ProfileManager and not NavigatePageMenu.Configuration )
		{
			Navigate( typeof( NotLoggedInView ), WindowHelper.Presets.NotLoggedIn, page, NavigationSlideAxis.Up );
			return;
		}
		if( page is NavigatePageMenu.HackFileManager or NavigatePageMenu.DependencyGraph && !AppComposition.Session.IsLoggedIn )
		{
			Navigate( typeof( NotLoggedInView ), WindowHelper.Presets.NotLoggedIn, page, NavigationSlideAxis.Up );
			return;
		}
		var (type, config, _) = Pages[ page ];
		NavigationSlideAxis slide = ChooseSlideDirection( _currentMenu, page );
		Navigate( type, config, page, slide );
	}

	private static NavigationSlideAxis ChooseSlideDirection( NavigatePageMenu from, NavigatePageMenu to )
	{
		if( from == to )
			return NavigationSlideAxis.Up;
		if( from == NavigatePageMenu.ProfileManager && to is NavigatePageMenu.HackFileManager or NavigatePageMenu.DependencyGraph )
			return NavigationSlideAxis.Left;
		if( Pages.TryGetValue( from, out var fromEntry ) && Pages.TryGetValue( to, out var toEntry ) )
			return toEntry.Order > fromEntry.Order ? NavigationSlideAxis.Up : NavigationSlideAxis.Down;
		return NavigationSlideAxis.Up;
	}

	private void Navigate( Type page, string config, NavigatePageMenu menu, NavigationSlideAxis slide )
	{
		// Resize before navigating so the destination page lays out once at its target dimensions.
		if( HackApp.Window is { } window )
			ApplyWindowPreset( window, config );

		if( NavFrame.CurrentSourcePageType == page )
			return;

		bool firstPage = NavFrame.Content is null;
		_currentMenu = menu;
		NavFrame.Navigate( page, null, new SuppressNavigationTransitionInfo() );
		if( !firstPage )
			NavigationTransitions.Play( NavFrame.Content, slide );
		PruneStaleBackStack( AppComposition.Session.IsLoggedIn );
		RailBackButton.IsEnabled = NavFrame.CanGoBack;
	}

	private void ApplyWindowPreset( Window window, string config )
	{
		var (minWidth, minHeight) = ActiveDrawerMinimums();
		WindowHelper.ApplyMainWindowPagePreset( window, config, minWidth, minHeight );
	}

	internal void CaptureLayoutState()
		=> ( NavFrame.Content as HackFileManagerView )?.CaptureLayoutState();

	private (int MinWidth, int MinHeight) ActiveDrawerMinimums()
	{
		int minWidth = !IsSearchDrawerOpen ? 0
			: UIStorage.WorkspaceLayout.SearchMinimalMode ? MinimalSearchMinWidth : FullSearchMinWidth;
		int minHeight = IsStatusDrawerOpen && !UIStorage.WorkspaceLayout.StatusMinimalMode ? FullStatusMinHeight : 0;
		return ( minWidth, minHeight );
	}

	private void PruneStaleBackStack( bool isLoggedIn )
	{
		for( int i = NavFrame.BackStack.Count - 1; i >= 0; i-- )
		{
			Type pageType = NavFrame.BackStack[ i ].SourcePageType;
			if( isLoggedIn && pageType == typeof( NotLoggedInView ) )
				NavFrame.BackStack.RemoveAt( i );
			else if( !isLoggedIn && ( pageType == typeof( HackFileManagerView ) || pageType == typeof( DependencyGraphView ) ) )
				NavFrame.BackStack.RemoveAt( i );
		}
	}

	private void GoBack()
	{
		PruneStaleBackStack( AppComposition.Session.IsLoggedIn );
		if( !NavFrame.CanGoBack )
		{
			RailBackButton.IsEnabled = false;
			return;
		}

		NavFrame.GoBack();
		RailBackButton.IsEnabled = NavFrame.CanGoBack;

		Type? current = NavFrame.CurrentSourcePageType;
		if( current == typeof( NotLoggedInView ) )
		{
			SetRailSelection( null );
			if( HackApp.Window is { } win )
				ApplyWindowPreset( win, WindowHelper.Presets.NotLoggedIn );
			return;
		}

		foreach( var (menu, entry) in Pages )
		{
			if( entry.Page != current )
				continue;
			_currentMenu = menu;
			SetRailSelection( menu );
			if( HackApp.Window is { } window )
				ApplyWindowPreset( window, entry.Config );
			NavigationTransitions.Play( NavFrame.Content, NavigationSlideAxis.Right );
			break;
		}
	}

	private void HomeView_KeyDown( object sender, KeyRoutedEventArgs e )
	{
		bool ctrl = KeyboardHelper.IsDown( VirtualKey.Control );
		bool alt = KeyboardHelper.IsDown( VirtualKey.Menu );

		if( alt && e.Key == VirtualKey.Left )
		{
			e.Handled = true;
			GoBack();
			return;
		}
		if( alt && e.Key == VirtualKey.S )
		{
			e.Handled = true;
			AppComposition.Dialogs.ToggleStatusWindow();
			return;
		}
		if( ctrl && e.Key == VirtualKey.F )
		{
			e.Handled = true;
			AppComposition.Dialogs.ToggleSearchWindow();
			return;
		}
		if( !ctrl )
			return;

		NavigatePageMenu? shortcut = e.Key switch
		{
			VirtualKey.Number1 or VirtualKey.NumberPad1 => NavigatePageMenu.HackFileManager,
			VirtualKey.Number2 or VirtualKey.NumberPad2 => NavigatePageMenu.DependencyGraph,
			VirtualKey.Number3 or VirtualKey.NumberPad3 => NavigatePageMenu.ProfileManager,
			VirtualKey.Number4 or VirtualKey.NumberPad4 => NavigatePageMenu.Configuration,
			VirtualKey.Number5 or VirtualKey.NumberPad5 => NavigatePageMenu.Settings,
			_ => null,
		};
		if( shortcut is { } target )
		{
			e.Handled = true;
			Select( target );
		}
	}

	#region Docked Extension Drawers (Status Log & Search Vault)

	internal void ShowStatusPanel( StatusDialogView page )
	{
		page.ParentWindow = null;
		page.SetHostMode( isDocked: true );
		page.SetMinimalMode( UIStorage.WorkspaceLayout.StatusMinimalMode );
		if( !ReferenceEquals( DockedStatusHost.Content, page ) )
			DockedStatusHost.Content = page;

		DockedStatusDrawer.Visibility = Visibility.Visible;
		ApplyStatusDrawerMode();
	}

	internal void ApplyStatusDrawerMode()
	{
		if( !IsStatusDrawerOpen )
			return;
		bool minimal = UIStorage.WorkspaceLayout.StatusMinimalMode;
		StatusPage?.SetMinimalMode( minimal );
		StatusDrawerHeader.Visibility = StatusSplitter.Visibility = ( !minimal ).ToVisibility();
		MiniStatusHostActions.Visibility = minimal.ToVisibility();

		if( minimal )
		{
			StatusDrawerRow.Height = GridLength.Auto;
			return;
		}
		StatusDrawerRow.Height = new GridLength( Math.Clamp( UIStorage.WorkspaceLayout.StatusDrawerHeight, 160, 560 ) );
		EnsureMinimumWindowDimensions( minWidth: 0, minHeight: FullStatusMinHeight );
	}

	internal void HideStatusPanelImmediate()
	{
		HideOverlay( _overlayElement );
		ShellOverlayCanvas.Children.Clear();
		DockedStatusDrawer.Visibility = StatusSplitter.Visibility = Visibility.Collapsed;
		StatusDrawerRow.Height = new GridLength( 0 );
	}

	internal StatusDialogView? TakeStatusPageForDetach()
	{
		StatusDialogView? page = DockedStatusHost.Content as StatusDialogView;
		DockedStatusHost.Content = null;
		HideStatusPanelImmediate();
		return page;
	}

	internal void ShowSearchPanel( SearchOdooView page )
	{
		if( AppComposition.Session.IsVersionMismatch )
			return;
		page.StoreWindowInstance( null );
		page.SetHostMode( isDocked: true );
		page.SetMinimalMode( UIStorage.WorkspaceLayout.SearchMinimalMode );
		if( !ReferenceEquals( DockedSearchHost.Content, page ) )
			DockedSearchHost.Content = page;

		DockedSearchDrawer.Visibility = Visibility.Visible;
		ArrangeSearchDrawer();
	}

	internal void HideSearchPanelImmediate()
	{
		DockedSearchDrawer.Visibility = SearchSplitter.Visibility = Visibility.Collapsed;
		ArrangeSearchDrawer();
	}

	internal SearchOdooView? TakeSearchPageForDetach()
	{
		SearchOdooView? page = DockedSearchHost.Content as SearchOdooView;
		DockedSearchHost.Content = null;
		HideSearchPanelImmediate();
		return page;
	}

	internal void ArrangeSearchDrawer()
	{
		MainAndSearchGrid.ColumnDefinitions.Clear();
		MainAndSearchGrid.RowDefinitions.Clear();
		_searchColumn = null;

		GridLength star = new( 1, GridUnitType.Star );
		if( !IsSearchDrawerOpen )
		{
			MainAndSearchGrid.RowDefinitions.Add( new RowDefinition { Height = star } );
			MainAndSearchGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = star } );
			NavFrame.SetGrid( 0, 0 );
			SearchSplitter.Visibility = Visibility.Collapsed;
			return;
		}

		bool minimal = UIStorage.WorkspaceLayout.SearchMinimalMode;
		SearchPage?.SetMinimalMode( minimal );
		SearchDrawerHeader.Visibility = ( !minimal ).ToVisibility();
		MiniSearchHostActions.Visibility = minimal.ToVisibility();
		SearchSplitter.Visibility = ( !minimal ).ToVisibility();

		if( minimal )
		{
			DockedSearchDrawer.BorderThickness = new Thickness( 0, 0, 0, 1 );
			MainAndSearchGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = star } );
			MainAndSearchGrid.RowDefinitions.Add( new RowDefinition { Height = GridLength.Auto } );
			MainAndSearchGrid.RowDefinitions.Add( new RowDefinition { Height = star } );
			DockedSearchDrawer.SetGrid( 0, 0 );
			NavFrame.SetGrid( 1, 0 );
			EnsureMinimumWindowDimensions( minWidth: MinimalSearchMinWidth, minHeight: 0 );
			return;
		}

		bool onLeft = UIStorage.WorkspaceLayout.SearchDock == FolderDockSide.Left;
		DockedSearchDrawer.BorderThickness = onLeft ? new Thickness( 0, 0, 1, 0 ) : new Thickness( 1, 0, 0, 0 );
		MainAndSearchGrid.RowDefinitions.Add( new RowDefinition { Height = star } );
		_searchColumn = new ColumnDefinition { Width = new GridLength( Math.Clamp( UIStorage.WorkspaceLayout.SearchDrawerWidth, 340, 760 ) ) };
		ColumnDefinition mainCol = new() { Width = star };

		MainAndSearchGrid.ColumnDefinitions.Add( onLeft ? _searchColumn : mainCol );
		MainAndSearchGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = GridLength.Auto } );
		MainAndSearchGrid.ColumnDefinitions.Add( onLeft ? mainCol : _searchColumn );
		DockedSearchDrawer.SetGrid( 0, onLeft ? 0 : 2 );
		SearchSplitter.SetGrid( 0, 1 );
		NavFrame.SetGrid( 0, onLeft ? 2 : 0 );
		EnsureMinimumWindowDimensions( minWidth: FullSearchMinWidth, minHeight: 0 );
	}

	private static void EnsureMinimumWindowDimensions( int minWidth, int minHeight )
	{
		if( HackApp.Window is { } window )
			WindowHelper.EnsureMinimumSize( window, minWidth, minHeight );
	}

	private void SearchSplitter_DragDelta( double delta )
	{
		if( _searchColumn is null )
			return;
		double sign = UIStorage.WorkspaceLayout.SearchDock == FolderDockSide.Left ? 1 : -1;
		double next = Math.Clamp( _searchColumn.Width.Value + delta * sign, 340, 760 );
		_searchColumn.Width = new GridLength( next );
		UIStorage.WorkspaceLayout.SearchDrawerWidth = next;
	}

	private void StatusSplitter_DragDelta( double delta )
	{
		double next = Math.Clamp( StatusDrawerRow.Height.Value - delta, 160, 560 );
		StatusDrawerRow.Height = new GridLength( next );
		UIStorage.WorkspaceLayout.StatusDrawerHeight = next;
	}

	private void ShellSplitter_DragCompleted() => SaveLayoutAsync();
	private void SwitchSearchMinimal_Click( object sender, RoutedEventArgs e ) => AppComposition.Dialogs.SetSearchMinimalMode( true );
	private void SwitchStatusMinimal_Click( object sender, RoutedEventArgs e ) => AppComposition.Dialogs.SetStatusMinimalMode( true );
	private void DetachSearch_Click( object sender, RoutedEventArgs e ) => AppComposition.Dialogs.DetachSearchWindow();
	private void CloseSearchDrawer_Click( object sender, RoutedEventArgs e ) => AppComposition.Dialogs.CloseSearchWindow();
	private void DetachStatus_Click( object sender, RoutedEventArgs e ) => AppComposition.Dialogs.DetachStatusWindow();

	private void SwapSearchSide_Click( object sender, RoutedEventArgs e )
	{
		UIStorage.WorkspaceLayout.SearchDock = UIStorage.WorkspaceLayout.SearchDock == FolderDockSide.Right
			? FolderDockSide.Left
			: FolderDockSide.Right;
		ArrangeSearchDrawer();
		SaveLayoutAsync();
	}

	private static void SaveLayoutAsync() => UIStorage.SaveSafe();

	private async void CloseStatusDrawer_Click( object sender, RoutedEventArgs e )
	{
		try
		{
			if( StatusPage is { } page )
				await page.RequestCloseAsync();
			else
				AppComposition.Dialogs.CloseStatusWindow();
		}
		catch( Exception ex )
		{
			HackLogger.Warn( "Shell", $"Closing status drawer failed: {ex.Message}", ex );
		}
	}

	private FrameworkElement? _overlayElement;
	private FrameworkElement? _overlayAnchor;
	private double _overlayWidth;
	private double _overlayFallbackHeight;

	internal void ShowOverlay( FrameworkElement element, FrameworkElement anchor, double width, double height )
	{
		if( !ReferenceEquals( _overlayElement, element ) )
		{
			if( _overlayElement is not null )
				_overlayElement.SizeChanged -= OverlayElement_SizeChanged;
			_overlayElement = element;
			element.SizeChanged += OverlayElement_SizeChanged;
		}
		_overlayAnchor = anchor;
		_overlayWidth = width;
		_overlayFallbackHeight = height;

		if( !ShellOverlayCanvas.Children.Contains( element ) )
			ShellOverlayCanvas.Children.Add( element );

		element.Measure( new Windows.Foundation.Size( width, 420 ) );
		RepositionOverlay();
	}

	private void OverlayElement_SizeChanged( object sender, SizeChangedEventArgs e ) => RepositionOverlay();

	private void RepositionOverlay()
	{
		if( _overlayElement is not { } element || _overlayAnchor is not { } anchor || !ShellOverlayCanvas.Children.Contains( element ) )
			return;

		double actualH = Math.Max( element.ActualHeight, element.DesiredSize.Height );
		if( actualH <= 0 )
			actualH = Math.Clamp( _overlayFallbackHeight, 110, 380 );

		Windows.Foundation.Point anchorPos;
		try
		{
			anchorPos = anchor.TransformToVisual( ShellOverlayCanvas ).TransformPoint( new Windows.Foundation.Point( 0, 0 ) );
		}
		catch
		{
			return;
		}
		double maxW = ShellOverlayCanvas.ActualWidth > 0 ? ShellOverlayCanvas.ActualWidth : ActualWidth;
		double left = Math.Clamp( anchorPos.X, 8, Math.Max( 8, maxW - _overlayWidth - 16 ) );
		double top = anchorPos.Y >= actualH + 14
			? anchorPos.Y - actualH - 8
			: anchorPos.Y + anchor.ActualHeight + 8;

		Canvas.SetLeft( element, left );
		Canvas.SetTop( element, top );
	}

	internal void HideOverlay( FrameworkElement? element )
	{
		if( element is null )
			return;
		if( ReferenceEquals( _overlayElement, element ) )
		{
			element.SizeChanged -= OverlayElement_SizeChanged;
			_overlayElement = null;
			_overlayAnchor = null;
		}
		ShellOverlayCanvas.Children.Remove( element );
	}

	#endregion
}
