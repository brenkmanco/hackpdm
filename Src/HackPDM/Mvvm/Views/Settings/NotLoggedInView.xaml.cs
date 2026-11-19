using HackPDM.Shared.GlobalData;
using HackPDM.Shell;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Mvvm.Views.Settings
{
	/// <summary>Shown when a workspace page is opened before signing in, with shortcuts to sign in or configure settings.</summary>
	public sealed partial class NotLoggedInView : Page
	{
		public NotLoggedInView()
		{
			InitializeComponent();
		}

		private void OpenProfile_Click( object sender, RoutedEventArgs e )
			=> AppComposition.Navigation.NavigateTo( NavigatePageMenu.ProfileManager );

		private void OpenConfig_Click( object sender, RoutedEventArgs e )
			=> AppComposition.Navigation.NavigateTo( NavigatePageMenu.Configuration );
	}
}
