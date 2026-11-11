using System.Linq;

using HackPDM.Domain.Representation;
using HackPDM.Mvvm.ViewModels.Odoo;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using HackPDM.Shell;

namespace HackPDM.Mvvm.Views.Odoo;

/// <summary>View for <see cref="SearchOdooViewModel"/>: binds the form and forwards selections.</summary>
public sealed partial class SearchOdooView : Page
{
	public SearchOdooViewModel ViewModel => AppComposition.SearchOdoo;

	private Window? _parentWindow;

	public SearchOdooView()
	{
		InitializeComponent();
		ViewModel.LoadLookups();

		OdooSearchResults.SelectionChanged += ( _, _ )
			=> ViewModel.SelectedResults = [ .. OdooSearchResults.SelectedItems.OfType<SearchRow>() ];
		OdooSearchPropList.SelectionChanged += ( _, _ )
			=> ViewModel.SelectedActiveProperties = [ .. OdooSearchPropList.SelectedItems.OfType<SearchPropertiesRow>() ];
	}

	public void StoreWindowInstance( Window window ) => _parentWindow = window;

	private void OdooCancel_Click( object sender, RoutedEventArgs e )
	{
		if( Frame?.CanGoBack is true )
			Frame.GoBack();
		else
			_parentWindow?.Close();
	}

	private void OdooSearchResults_DoubleTapped( object sender, DoubleTappedRoutedEventArgs e )
		=> ViewModel.LocateCommand.Execute( OdooSearchResults.SelectedItem as SearchRow );
}
