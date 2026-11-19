using System;
using System.Collections.Generic;
using System.Linq;

using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Mvvm.Views.Common.Settings;
using HackPDM.Mvvm.Views.Odoo;
using HackPDM.Mvvm.Views.Settings;
using HackPDM.Mvvm.Views.Tools;
using HackPDM.Shared.GlobalData;
using HackPDM.Shell;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;

using Windows.System;
using Windows.UI.Core;

namespace HackPDM.Mvvm.Views.Hack;

/// <summary>
/// The main window's page: a navigation menu and the frame showing the chosen page. Each menu item's
/// Tag is its <see cref="NavigatePageMenu"/> name; <see cref="NavigationService"/> selects items from
/// view models.
/// </summary>
public sealed partial class HomeView : Page
{
	/// <summary>Each page and the window size, title, and menu order it uses.</summary>
	private static readonly Dictionary<NavigatePageMenu, (Type Page, string Config, int Order)> Pages = new()
	{
		[ NavigatePageMenu.HackFileManager ] = ( typeof( HackFileManagerView ), "HackFileManager", 0 ),
		[ NavigatePageMenu.DependencyGraph ] = ( typeof( DependencyGraphView ), "DependencyGraph", 1 ),
		[ NavigatePageMenu.ProfileManager ] = ( typeof( ProfileManagerView ), "ProfileManager", 2 ),
		[ NavigatePageMenu.Configuration ] = ( typeof( ConfigurationView ), "ConfigSettings", 3 ),
		[ NavigatePageMenu.JsonEditor ] = ( typeof( JsonEditorView ), "DynamicJsonForm", 4 ),
		[ NavigatePageMenu.Settings ] = ( typeof( ApplicationSettingsView ), "AppSettings", 5 ),
	};

	private bool _syncingSelection;
	private NavigatePageMenu _currentMenu = NavigatePageMenu.ProfileManager;
	private ColumnDefinition? _searchColumn;

	public HomeView()
	{
		InitializeComponent();
		AppComposition.Navigation.Attach( this );
		HomeNavigator.SelectedItem = ItemFor( NavigatePageMenu.ProfileManager );
		NavFrame.Navigated += ( _, _ ) => HomeNavigator.IsBackEnabled = NavFrame.CanGoBack;
		ArrangeSearchDrawer();
	}

	/// <summary>The frame that hosts the page selected in the navigation menu.</summary>
	internal Frame ContentFrame => NavFrame;

	internal StatusDialogView? StatusPage => DockedStatusHost.Content as StatusDialogView;
	internal SearchOdooView? SearchPage => DockedSearchHost.Content as SearchOdooView;
	internal bool IsStatusDrawerOpen => DockedStatusDrawer.Visibility == Microsoft.UI.Xaml.Visibility.Visible;
	internal bool IsSearchDrawerOpen => DockedSearchDrawer.Visibility == Microsoft.UI.Xaml.Visibility.Visible;

	/// <summary>Selects the page's menu item, which shows the page.</summary>
	internal void Select( NavigatePageMenu page )
	{
		object? target = page == NavigatePageMenu.Settings
			? HomeNavigator.SettingsItem
			: ItemFor( page ) ?? ItemFor( NavigatePageMenu.ProfileManager );

		// Re-selecting a locked tab (e.g. NotLoggedInView) after signing in must still show the real page
		if( ReferenceEquals( HomeNavigator.SelectedItem, target ) )
		{
			Show( page );
			return;
		}
		HomeNavigator.SelectedItem = target;
	}

	private NavigationViewItem? ItemFor( NavigatePageMenu page )
		=> HomeNavigator.MenuItems.OfType<NavigationViewItem>().FirstOrDefault( item => item.Tag as string == page.ToString() );

	private void HomeNavigator_ItemInvoked( NavigationView sender, NavigationViewItemInvokedEventArgs args )
	{
		if( args.InvokedItemContainer == StatusLogNav )
			AppComposition.Dialogs.ToggleStatusWindow();
		else if( args.InvokedItemContainer == SearchNav )
			AppComposition.Dialogs.ToggleSearchWindow();
	}

	private void HomeNavigator_SelectionChanged( NavigationView sender, NavigationViewSelectionChangedEventArgs args )
	{
		if( _syncingSelection )
			return;
		if( args.IsSettingsSelected )
			Show( NavigatePageMenu.Settings );
		else if( args.SelectedItem is NavigationViewItem { Tag: string tag } && Enum.TryParse( tag, out NavigatePageMenu page ) )
			Show( page );
	}

	private void Show( NavigatePageMenu page )
	{
		if( AppComposition.Session.IsVersionMismatch && page != NavigatePageMenu.ProfileManager )
		{
			Navigate( typeof( NotLoggedInView ), "NotLoggedIn", page, NavigationSlideAxis.Up );
			return;
		}
		if( page is NavigatePageMenu.HackFileManager or NavigatePageMenu.DependencyGraph && !AppComposition.Session.IsLoggedIn )
		{
			Navigate( typeof( NotLoggedInView ), "NotLoggedIn", page, NavigationSlideAxis.Up );
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
			WindowHelper.SetWindowConfig( window, WindowConfig.Preset( config ) );

		if( NavFrame.CurrentSourcePageType == page )
			return;

		bool firstPage = NavFrame.Content is null;
		_currentMenu = menu;
		NavFrame.Navigate( page, null, new SuppressNavigationTransitionInfo() );
		if( !firstPage )
			NavigationTransitions.Play( NavFrame.Content, slide );
		PruneStaleBackStack( AppComposition.Session.IsLoggedIn );
		HomeNavigator.IsBackEnabled = NavFrame.CanGoBack;
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

	private void HomeNavigator_BackRequested( NavigationView sender, NavigationViewBackRequestedEventArgs args )
		=> GoBack();

	private void GoBack()
	{
		PruneStaleBackStack( AppComposition.Session.IsLoggedIn );
		if( !NavFrame.CanGoBack )
		{
			HomeNavigator.IsBackEnabled = false;
			return;
		}

		NavFrame.GoBack();
		HomeNavigator.IsBackEnabled = NavFrame.CanGoBack;

		Type? current = NavFrame.CurrentSourcePageType;
		if( current == typeof( NotLoggedInView ) )
		{
			if( HackApp.Window is { } win )
				WindowHelper.SetWindowConfig( win, WindowConfig.Preset( "NotLoggedIn" ) );
			return;
		}

		foreach( var (menu, entry) in Pages )
		{
			if( entry.Page != current )
				continue;
			_currentMenu = menu;
			_syncingSelection = true;
			try
			{
				if( menu == NavigatePageMenu.Settings )
					HomeNavigator.SelectedItem = HomeNavigator.SettingsItem;
				else if( ItemFor( menu ) is { } navItem )
					HomeNavigator.SelectedItem = navItem;
			}
			finally
			{
				_syncingSelection = false;
			}
			if( HackApp.Window is { } window )
				WindowHelper.SetWindowConfig( window, WindowConfig.Preset( entry.Config ) );
			NavigationTransitions.Play( NavFrame.Content, NavigationSlideAxis.Right );
			break;
		}
	}

	private void HomeView_KeyDown( object sender, KeyRoutedEventArgs e )
	{
		bool ctrl = InputKeyboardSource.GetKeyStateForCurrentThread( VirtualKey.Control ).HasFlag( CoreVirtualKeyStates.Down );
		bool alt = InputKeyboardSource.GetKeyStateForCurrentThread( VirtualKey.Menu ).HasFlag( CoreVirtualKeyStates.Down );

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
			AppComposition.Dialogs.ShowSearchWindow();
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
			VirtualKey.Number5 or VirtualKey.NumberPad5 => NavigatePageMenu.JsonEditor,
			VirtualKey.Number6 or VirtualKey.NumberPad6 => NavigatePageMenu.Settings,
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

		DockedStatusDrawer.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
		ApplyStatusDrawerMode();
	}

	internal void ApplyStatusDrawerMode()
	{
		if( !IsStatusDrawerOpen )
			return;
		bool minimal = UIStorage.WorkspaceLayout.StatusMinimalMode;
		StatusPage?.SetMinimalMode( minimal );
		StatusDrawerHeader.Visibility = minimal
			? Microsoft.UI.Xaml.Visibility.Collapsed
			: Microsoft.UI.Xaml.Visibility.Visible;
		MiniStatusHostActions.Visibility = minimal
			? Microsoft.UI.Xaml.Visibility.Visible
			: Microsoft.UI.Xaml.Visibility.Collapsed;
		StatusSplitter.Visibility = minimal
			? Microsoft.UI.Xaml.Visibility.Collapsed
			: Microsoft.UI.Xaml.Visibility.Visible;

		if( minimal )
		{
			StatusDrawerRow.Height = Microsoft.UI.Xaml.GridLength.Auto;
		}
		else
		{
			double height = Math.Clamp( UIStorage.WorkspaceLayout.StatusDrawerHeight, 180, 520 );
			StatusDrawerRow.Height = new Microsoft.UI.Xaml.GridLength( height );
			EnsureMinimumWindowDimensions( minWidth: 0, minHeight: 720 );
		}
	}

	internal void HideStatusPanelImmediate()
	{
		ShellOverlayCanvas.Children.Clear();
		DockedStatusDrawer.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
		StatusSplitter.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
		StatusDrawerRow.Height = new Microsoft.UI.Xaml.GridLength( 0 );
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
		page.StoreWindowInstance( null );
		page.SetHostMode( isDocked: true );
		page.SetMinimalMode( UIStorage.WorkspaceLayout.SearchMinimalMode );
		if( AppComposition.Session.IsVersionMismatch )
			return;
		if( !ReferenceEquals( DockedSearchHost.Content, page ) )
			DockedSearchHost.Content = page;

		DockedSearchDrawer.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
		ArrangeSearchDrawer();
	}

	internal void HideSearchPanelImmediate()
	{
		DockedSearchDrawer.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
		SearchSplitter.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
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

		bool showSearch = DockedSearchDrawer.Visibility == Microsoft.UI.Xaml.Visibility.Visible;
		if( !showSearch )
		{
			MainAndSearchGrid.RowDefinitions.Add( new RowDefinition { Height = new Microsoft.UI.Xaml.GridLength( 1, Microsoft.UI.Xaml.GridUnitType.Star ) } );
			MainAndSearchGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = new Microsoft.UI.Xaml.GridLength( 1, Microsoft.UI.Xaml.GridUnitType.Star ) } );
			Grid.SetRow( NavFrame, 0 );
			Grid.SetColumn( NavFrame, 0 );
			SearchSplitter.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
			return;
		}

		bool minimal = UIStorage.WorkspaceLayout.SearchMinimalMode;
		SearchPage?.SetMinimalMode( minimal );
		SearchDrawerHeader.Visibility = minimal
			? Microsoft.UI.Xaml.Visibility.Collapsed
			: Microsoft.UI.Xaml.Visibility.Visible;
		MiniSearchHostActions.Visibility = minimal
			? Microsoft.UI.Xaml.Visibility.Visible
			: Microsoft.UI.Xaml.Visibility.Collapsed;

		if( minimal )
		{
			SearchSplitter.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
			DockedSearchDrawer.BorderThickness = new Microsoft.UI.Xaml.Thickness( 0, 0, 0, 1 );
			MainAndSearchGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = new Microsoft.UI.Xaml.GridLength( 1, Microsoft.UI.Xaml.GridUnitType.Star ) } );
			MainAndSearchGrid.RowDefinitions.Add( new RowDefinition { Height = Microsoft.UI.Xaml.GridLength.Auto } );
			MainAndSearchGrid.RowDefinitions.Add( new RowDefinition { Height = new Microsoft.UI.Xaml.GridLength( 1, Microsoft.UI.Xaml.GridUnitType.Star ) } );
			Grid.SetRow( DockedSearchDrawer, 0 );
			Grid.SetColumn( DockedSearchDrawer, 0 );
			Grid.SetRow( NavFrame, 1 );
			Grid.SetColumn( NavFrame, 0 );
			EnsureMinimumWindowDimensions( minWidth: 960, minHeight: 0 );
			return;
		}

		SearchSplitter.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
		double width = Math.Clamp( UIStorage.WorkspaceLayout.SearchDrawerWidth, 340, 760 );
		bool onLeft = UIStorage.WorkspaceLayout.SearchDock == FolderDockSide.Left;
		DockedSearchDrawer.BorderThickness = onLeft
			? new Microsoft.UI.Xaml.Thickness( 0, 0, 1, 0 )
			: new Microsoft.UI.Xaml.Thickness( 1, 0, 0, 0 );

		MainAndSearchGrid.RowDefinitions.Add( new RowDefinition { Height = new Microsoft.UI.Xaml.GridLength( 1, Microsoft.UI.Xaml.GridUnitType.Star ) } );
		_searchColumn = new ColumnDefinition { Width = new Microsoft.UI.Xaml.GridLength( width ) };
		Grid.SetRow( DockedSearchDrawer, 0 );
		Grid.SetRow( SearchSplitter, 0 );
		Grid.SetRow( NavFrame, 0 );
		if( onLeft )
		{
			MainAndSearchGrid.ColumnDefinitions.Add( _searchColumn );
			MainAndSearchGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = Microsoft.UI.Xaml.GridLength.Auto } );
			MainAndSearchGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = new Microsoft.UI.Xaml.GridLength( 1, Microsoft.UI.Xaml.GridUnitType.Star ) } );
			Grid.SetColumn( DockedSearchDrawer, 0 );
			Grid.SetColumn( SearchSplitter, 1 );
			Grid.SetColumn( NavFrame, 2 );
		}
		else
		{
			MainAndSearchGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = new Microsoft.UI.Xaml.GridLength( 1, Microsoft.UI.Xaml.GridUnitType.Star ) } );
			MainAndSearchGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = Microsoft.UI.Xaml.GridLength.Auto } );
			MainAndSearchGrid.ColumnDefinitions.Add( _searchColumn );
			Grid.SetColumn( NavFrame, 0 );
			Grid.SetColumn( SearchSplitter, 1 );
			Grid.SetColumn( DockedSearchDrawer, 2 );
		}
		EnsureMinimumWindowDimensions( minWidth: 1120, minHeight: 0 );
	}

	private static void EnsureMinimumWindowDimensions( int minWidth, int minHeight )
	{
		if( HackApp.Window?.AppWindow is not { } appWindow )
			return;
		var size = appWindow.Size;
		int w = minWidth > 0 ? Math.Max( size.Width, minWidth ) : size.Width;
		int h = minHeight > 0 ? Math.Max( size.Height, minHeight ) : size.Height;
		if( w != size.Width || h != size.Height )
			appWindow.Resize( new Windows.Graphics.SizeInt32 { Width = w, Height = h } );
	}

	private void SearchSplitter_DragDelta( double delta )
	{
		if( _searchColumn is null )
			return;
		double sign = UIStorage.WorkspaceLayout.SearchDock == FolderDockSide.Left ? 1 : -1;
		double next = Math.Clamp( _searchColumn.Width.Value + delta * sign, 340, 760 );
		_searchColumn.Width = new Microsoft.UI.Xaml.GridLength( next );
		UIStorage.WorkspaceLayout.SearchDrawerWidth = next;
	}

	private void StatusSplitter_DragDelta( double delta )
	{
		double next = Math.Clamp( StatusDrawerRow.Height.Value - delta, 160, 560 );
		StatusDrawerRow.Height = new Microsoft.UI.Xaml.GridLength( next );
		UIStorage.WorkspaceLayout.StatusDrawerHeight = next;
	}

	private void ShellSplitter_DragCompleted()
	{
		try
		{
			AppConfigStore.Save( UIStorage.GlobalData );
		}
		catch { }
	}

	private void SwitchSearchMinimal_Click( object sender, Microsoft.UI.Xaml.RoutedEventArgs e )
		=> AppComposition.Dialogs.SetSearchMinimalMode( true );

	private void SwitchStatusMinimal_Click( object sender, Microsoft.UI.Xaml.RoutedEventArgs e )
		=> AppComposition.Dialogs.SetStatusMinimalMode( true );

	private void SwapSearchSide_Click( object sender, Microsoft.UI.Xaml.RoutedEventArgs e )
	{
		UIStorage.WorkspaceLayout.SearchDock = UIStorage.WorkspaceLayout.SearchDock == FolderDockSide.Right
			? FolderDockSide.Left
			: FolderDockSide.Right;
		ArrangeSearchDrawer();
		ShellSplitter_DragCompleted();
	}

	private void DetachSearch_Click( object sender, Microsoft.UI.Xaml.RoutedEventArgs e )
		=> AppComposition.Dialogs.DetachSearchWindow();

	private void CloseSearchDrawer_Click( object sender, Microsoft.UI.Xaml.RoutedEventArgs e )
		=> AppComposition.Dialogs.CloseSearchWindow();

	private void DetachStatus_Click( object sender, Microsoft.UI.Xaml.RoutedEventArgs e )
		=> AppComposition.Dialogs.DetachStatusWindow();

	private async void CloseStatusDrawer_Click( object sender, Microsoft.UI.Xaml.RoutedEventArgs e )
	{
		if( StatusPage is { } page )
			await page.RequestCloseAsync();
		else
			AppComposition.Dialogs.CloseStatusWindow();
	}

	private Microsoft.UI.Xaml.FrameworkElement? _overlayElement;
	private Microsoft.UI.Xaml.FrameworkElement? _overlayAnchor;
	private double _overlayWidth;
	private double _overlayFallbackHeight;

	internal void ShowOverlay( Microsoft.UI.Xaml.FrameworkElement element, Microsoft.UI.Xaml.FrameworkElement anchor, double width, double height )
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

		ShellOverlayCanvas.UpdateLayout();
		element.Measure( new Windows.Foundation.Size( width, 420 ) );
		RepositionOverlay();
	}

	private void OverlayElement_SizeChanged( object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e )
		=> RepositionOverlay();

	private void RepositionOverlay()
	{
		if( _overlayElement is not { } element || _overlayAnchor is not { } anchor )
			return;
		if( !ShellOverlayCanvas.Children.Contains( element ) )
			return;

		double actualH = Math.Max( element.ActualHeight, element.DesiredSize.Height );
		if( actualH <= 0 )
			actualH = Math.Clamp( _overlayFallbackHeight, 110, 380 );

		var anchorPos = anchor.TransformToVisual( ShellOverlayCanvas ).TransformPoint( new Windows.Foundation.Point( 0, 0 ) );
		double maxW = ShellOverlayCanvas.ActualWidth > 0 ? ShellOverlayCanvas.ActualWidth : ActualWidth;
		double left = Math.Clamp( anchorPos.X, 8, Math.Max( 8, maxW - _overlayWidth - 16 ) );
		double top = anchorPos.Y >= actualH + 14
			? anchorPos.Y - actualH - 8
			: anchorPos.Y + anchor.ActualHeight + 8;

		Canvas.SetLeft( element, left );
		Canvas.SetTop( element, top );
	}

	internal void HideOverlay( Microsoft.UI.Xaml.FrameworkElement? element )
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
