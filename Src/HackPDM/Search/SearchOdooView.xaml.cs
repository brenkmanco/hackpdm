using HackPDM.Common;
using HackPDM.Core.Services;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using WinUI.TableView;

namespace HackPDM.Search;

/// <summary>View for <see cref="SearchOdooViewModel"/>: supports both full search and compact bar + quick-jump combolist.</summary>
public sealed partial class SearchOdooView : Page, IDockablePanel
{
	public SearchOdooViewModel ViewModel => AppComposition.SearchOdoo;

	private Window? _parentWindow;
	private bool _isMinimal;
	private bool _suppressComboNavigate;
	private bool? _lastCompact;
	private bool? _lastVeryNarrow;

	public SearchOdooView()
	{
		InitializeComponent();
		ViewModel.LoadLookups();

		OdooSearchResults.SelectionChanged += ( _, _ ) =>
		{
			if( !OdooSearchResults.IsUpdatingSelection )
				ViewModel.SelectedResults = OdooSearchResults.SelectedItems.ExtractSelected<SearchRow>();
		};
		OdooSearchResults.SelectionBatchCompleted += ( _, _ ) =>
			ViewModel.SelectedResults = OdooSearchResults.SelectedItems.ExtractSelected<SearchRow>();
		OdooSearchPropList.SelectionChanged += ( _, _ ) =>
			ViewModel.SelectedActiveProperties = OdooSearchPropList.SelectedItems.ExtractSelected<SearchPropertiesRow>();
		KeyDown += SearchOdooView_KeyDown;
		SizeChanged += ( _, _ ) => UpdateResponsiveLayout();
		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
	}

	internal bool IsMinimalMode => _isMinimal;

	public Window? HostWindow
	{
		get => _parentWindow;
		set => StoreWindowInstance( value );
	}

	/// <summary>Remembers the window this page is shown in, or clears it when docked inside the main window.</summary>
	public void StoreWindowInstance( Window? window )
	{
		_parentWindow = window;
		UpdateHostButtons();
	}

	public void SetHostMode( bool isDocked )
	{
		if( isDocked )
			_parentWindow = null;
		UpdateHostButtons();
		UpdateResponsiveLayout();
	}

	public void SetMinimalMode( bool minimal )
	{
		_isMinimal = minimal;
		FullSearchRoot.Visibility = ( !minimal ).ToVisibility();
		MinimalSearchRoot.Visibility = minimal.ToVisibility();
		UpdateHostButtons();
		UpdateResponsiveLayout();
	}

	private void UpdateHostButtons()
	{
		bool canDock = _parentWindow is not null && HackApp.HomePage is not null;
		btnDock.Visibility = canDock.ToVisibility();
		MiniDockBtn.Visibility = canDock.ToVisibility();
		MiniCloseBtn.Visibility = ( _parentWindow is not null ).ToVisibility();
	}

	private void OnLoaded( object sender, RoutedEventArgs e )
	{
		ViewModel.LoadLookups();
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
		bool veryNarrow = compact && ActualWidth < 430;
		if( _lastCompact == compact && _lastVeryNarrow == veryNarrow )
			return;
		bool compactChanged = _lastCompact != compact;
		_lastCompact = compact;
		_lastVeryNarrow = veryNarrow;

		if( compactChanged )
		{
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

				FilenameSection.SetGrid( 0, 0 );
				QueryDivider.Width = double.NaN;
				QueryDivider.Height = 1;
				QueryDivider.SetGrid( 1, 0 );
				PropertySection.SetGrid( 2, 0 );
			}
			else
			{
				QueryLayoutGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 1, GridUnitType.Star ) } );
				QueryLayoutGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = GridLength.Auto } );
				QueryLayoutGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 1.15, GridUnitType.Star ) } );
				QueryLayoutGrid.RowDefinitions.Add( new RowDefinition { Height = GridLength.Auto } );

				FilenameSection.SetGrid( 0, 0 );
				QueryDivider.Width = 1;
				QueryDivider.Height = double.NaN;
				QueryDivider.SetGrid( 0, 1 );
				PropertySection.SetGrid( 0, 2 );
			}
		}

		if( compact )
		{
			CheckboxesPanel.Orientation = veryNarrow ? Orientation.Vertical : Orientation.Horizontal;
			CheckboxesPanel.Spacing = veryNarrow ? 4 : 8;
		}
		else
		{
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
	{
		if( ( e.OriginalSource as DependencyObject ).FindAncestor<TableViewRow>( OdooSearchResults ) is not null )
			LocateAndFocusMainWindow( OdooSearchResults.SelectedItem as SearchRow );
	}

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
