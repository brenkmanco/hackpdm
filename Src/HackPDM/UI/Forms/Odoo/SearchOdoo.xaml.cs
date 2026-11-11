using System.Linq;

using HackPDM.Domain.Representation;
using HackPDM.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace HackPDM.UI.Forms.Odoo;

/// <summary>View for <see cref="SearchOdooViewModel"/>: binds the form and forwards selections.</summary>
public sealed partial class SearchOdoo : Page
{
	public SearchOdooViewModel ViewModel => InstanceManager.SearchOdoo;

	private Window? _parentWindow;

	public SearchOdoo()
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
