using System.Linq;

using HackPDM.Domain.Representation;
using HackPDM.Mvvm.ViewModels.Odoo;
using HackPDM.Shell;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace HackPDM.Mvvm.Views.Odoo;

/// <summary>View for <see cref="SearchOdooViewModel"/>: supports both full search and compact bar + quick-jump combolist.</summary>
public sealed partial class SearchOdooView : Page
{
	public SearchOdooViewModel ViewModel => AppComposition.SearchOdoo;

	private Window? _parentWindow;
	private bool _isMinimal;
	private bool _suppressComboNavigate;

	public SearchOdooView()
	{
		InitializeComponent();
		ViewModel.LoadLookups();

		OdooSearchResults.SelectionChanged += ( _, _ )
			=> ViewModel.SelectedResults = [ .. OdooSearchResults.SelectedItems.OfType<SearchRow>() ];
		OdooSearchPropList.SelectionChanged += ( _, _ )
			=> ViewModel.SelectedActiveProperties = [ .. OdooSearchPropList.SelectedItems.OfType<SearchPropertiesRow>() ];
		KeyDown += SearchOdooView_KeyDown;
		SizeChanged += ( _, _ ) => UpdateResponsiveLayout();
		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
	}

	internal bool IsMinimalMode => _isMinimal;

	/// <summary>Remembers the window this page is shown in, or clears it when docked inside the main window.</summary>
	public void StoreWindowInstance( Window? window )
	{
		_parentWindow = window;
		UpdateHostButtons();
	}

	internal void SetHostMode( bool isDocked )
	{
		if( isDocked )
			_parentWindow = null;
		UpdateHostButtons();
		UpdateResponsiveLayout();
	}

	internal void SetMinimalMode( bool minimal )
	{
		_isMinimal = minimal;
		FullSearchRoot.Visibility = minimal ? Visibility.Collapsed : Visibility.Visible;
		MinimalSearchRoot.Visibility = minimal ? Visibility.Visible : Visibility.Collapsed;
		UpdateHostButtons();
		UpdateResponsiveLayout();
	}

	private void UpdateHostButtons()
	{
		bool canDock = _parentWindow is not null && HackApp.HomePage is not null;
		btnDock.Visibility = canDock ? Visibility.Visible : Visibility.Collapsed;
		MiniDockBtn.Visibility = canDock ? Visibility.Visible : Visibility.Collapsed;
		MiniCloseBtn.Visibility = _parentWindow is not null ? Visibility.Visible : Visibility.Collapsed;
	}

	private void OnLoaded( object sender, RoutedEventArgs e )
	{
		ViewModel.SearchCompleted -= OnSearchCompleted;
		ViewModel.SearchCompleted += OnSearchCompleted;
	}

	private void OnUnloaded( object sender, RoutedEventArgs e )
	{
		ViewModel.SearchCompleted -= OnSearchCompleted;
		MiniResultsCombo.IsDropDownOpen = false;
	}

	private void OnSearchCompleted( int count )
	{
		if( !_isMinimal )
			return;
		_suppressComboNavigate = true;
		try
		{
			MiniResultsCombo.SelectedIndex = -1;
			MiniLocateBtn.IsEnabled = false;
		}
		finally
		{
			_suppressComboNavigate = false;
		}
		if( count > 0 && IsLoaded )
			MiniResultsCombo.IsDropDownOpen = true;
	}

	private void UpdateResponsiveLayout()
	{
		if( _isMinimal )
			return;
		bool compact = ActualWidth > 0 && ActualWidth < 860;
		FullSearchRoot.Margin = compact ? new Thickness( 10 ) : new Thickness( 16 );
		FullSearchRoot.RowSpacing = compact ? 8 : 12;
		QueryBuilderCard.Padding = compact ? new Thickness( 10 ) : new Thickness( 14 );
		OdooSearchPropList.Height = compact ? 84 : 110;
		btnSearch.MinWidth = compact ? 110 : 200;

		QueryLayoutGrid.ColumnDefinitions.Clear();
		QueryLayoutGrid.RowDefinitions.Clear();
		if( compact )
		{
			QueryLayoutGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 1, GridUnitType.Star ) } );
			QueryLayoutGrid.RowDefinitions.Add( new RowDefinition { Height = GridLength.Auto } );
			QueryLayoutGrid.RowDefinitions.Add( new RowDefinition { Height = GridLength.Auto } );
			QueryLayoutGrid.RowDefinitions.Add( new RowDefinition { Height = GridLength.Auto } );

			Grid.SetRow( FilenameSection, 0 );
			Grid.SetColumn( FilenameSection, 0 );
			QueryDivider.Width = double.NaN;
			QueryDivider.Height = 1;
			Grid.SetRow( QueryDivider, 1 );
			Grid.SetColumn( QueryDivider, 0 );
			Grid.SetRow( PropertySection, 2 );
			Grid.SetColumn( PropertySection, 0 );
			CheckboxesPanel.Orientation = ActualWidth < 430 ? Orientation.Vertical : Orientation.Horizontal;
			CheckboxesPanel.Spacing = ActualWidth < 430 ? 4 : 8;
		}
		else
		{
			QueryLayoutGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 1, GridUnitType.Star ) } );
			QueryLayoutGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = GridLength.Auto } );
			QueryLayoutGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 1.15, GridUnitType.Star ) } );
			QueryLayoutGrid.RowDefinitions.Add( new RowDefinition { Height = GridLength.Auto } );

			Grid.SetRow( FilenameSection, 0 );
			Grid.SetColumn( FilenameSection, 0 );
			QueryDivider.Width = 1;
			QueryDivider.Height = double.NaN;
			Grid.SetRow( QueryDivider, 0 );
			Grid.SetColumn( QueryDivider, 1 );
			Grid.SetRow( PropertySection, 0 );
			Grid.SetColumn( PropertySection, 2 );
			CheckboxesPanel.Orientation = Orientation.Horizontal;
			CheckboxesPanel.Spacing = 16;
		}
	}

	private void SwitchToMinimalSearch_Click( object sender, RoutedEventArgs e )
		=> AppComposition.Dialogs.SetSearchMinimalMode( true );

	private void OpenFullSearch_Click( object sender, RoutedEventArgs e )
	{
		MiniResultsCombo.IsDropDownOpen = false;
		AppComposition.Dialogs.ShowFullSearchWindow();
	}

	private void MiniResultsCombo_SelectionChanged( object sender, SelectionChangedEventArgs e )
	{
		SearchRow? row = MiniResultsCombo.SelectedItem as SearchRow;
		MiniLocateBtn.IsEnabled = row is not null;
		if( !_suppressComboNavigate && row is not null )
			LocateAndFocusMainWindow( row );
	}

	private void MiniLocate_Click( object sender, RoutedEventArgs e )
		=> LocateAndFocusMainWindow( MiniResultsCombo.SelectedItem as SearchRow );

	private void OdooDock_Click( object sender, RoutedEventArgs e )
		=> AppComposition.Dialogs.DockSearchWindow();

	private void OdooCancel_Click( object sender, RoutedEventArgs e )
	{
		MiniResultsCombo.IsDropDownOpen = false;
		if( Frame?.CanGoBack is true )
			Frame.GoBack();
		else if( _parentWindow is not null )
			_parentWindow.Close();
		else
			AppComposition.Dialogs.CloseSearchWindow();
	}

	private void OdooSearchResults_DoubleTapped( object sender, DoubleTappedRoutedEventArgs e )
		=> LocateAndFocusMainWindow( OdooSearchResults.SelectedItem as SearchRow );

	private void LocateSelected_Click( object sender, RoutedEventArgs e )
		=> LocateAndFocusMainWindow( OdooSearchResults.SelectedItem as SearchRow );

	private void LocateAndFocusMainWindow( SearchRow? row )
	{
		if( row is null )
			return;
		ViewModel.LocateCommand.Execute( row );
		if( _parentWindow is not null )
			HackApp.Window?.Activate();
	}

	private void SearchOdooView_KeyDown( object sender, KeyRoutedEventArgs e )
	{
		if( e.Key == Windows.System.VirtualKey.Escape )
		{
			e.Handled = true;
			OdooCancel_Click( sender, e );
		}
	}

	private void FileNameTextbox_KeyDown( object sender, KeyRoutedEventArgs e )
	{
		if( e.Key == Windows.System.VirtualKey.Enter && ViewModel.SearchCommand.CanExecute( null ) )
		{
			e.Handled = true;
			ViewModel.SearchCommand.Execute( null );
		}
	}

	private void OdooSearchPropValue_KeyDown( object sender, KeyRoutedEventArgs e )
	{
		if( e.Key == Windows.System.VirtualKey.Enter && ViewModel.AddPropertyCommand.CanExecute( null ) )
		{
			e.Handled = true;
			ViewModel.AddPropertyCommand.Execute( null );
		}
	}
}
