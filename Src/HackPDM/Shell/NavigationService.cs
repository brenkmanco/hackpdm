using System;

using HackPDM.Core.Pdm;

namespace HackPDM.Shell;

public interface INavigationService
{
	void NavigateTo( NavigatePageMenu page );
}

/// <summary>Navigation through the home page's menu. Does nothing until the home page exists.</summary>
public sealed class NavigationService : INavigationService
{
	private WeakReference<HomeView>? _home;

	internal void Attach( HomeView home ) => _home = new( home );

	public void NavigateTo( NavigatePageMenu page )
	{
		if( _home is not null && _home.TryGetTarget( out HomeView? home ) )
			home.Select( page );
	}
}
