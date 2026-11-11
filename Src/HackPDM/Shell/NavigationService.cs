using System;

using HackPDM.Mvvm.ViewModels.Common;
using HackPDM.Mvvm.Views.Hack;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Shell;

/// <summary>Navigation through the home page's menu. Does nothing until the home page exists.</summary>
public sealed class NavigationService : INavigationService
{
	private WeakReference<HomeView>? _home;

	/// <summary>Called by the home page once it is built.</summary>
	internal void Attach( HomeView home ) => _home = new( home );

	public void NavigateTo( NavigatePageMenu page )
	{
		if( _home is not null && _home.TryGetTarget( out HomeView? home ) )
			home.Select( page );
	}
}
