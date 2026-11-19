using HackPDM.Core.Pdm;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Shell;

public sealed partial class NotLoggedInView : Page
{
	public NotLoggedInView() => InitializeComponent();

	private void OpenProfile_Click( object sender, RoutedEventArgs e )
		=> AppComposition.Navigation.NavigateTo( NavigatePageMenu.ProfileManager );

	private void OpenConfig_Click( object sender, RoutedEventArgs e )
		=> AppComposition.Navigation.NavigateTo( NavigatePageMenu.Configuration );
}
