using System;
using System.Threading.Tasks;

using HackPDM.Mvvm.ViewModels.Common;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Mvvm.Views.Common.Settings;
using HackPDM.Mvvm.Views.Odoo;
using HackPDM.Mvvm.Views.Settings;
using HackPDM.Shared.GlobalData;
using HackPDM.Shell;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using MessageBox = HackPDM.Mvvm.Views.Dialogs.MessageBox;
using WindowHelper = HackPDM.Mvvm.Views.Common.Controls.WindowHelper;

namespace HackPDM.Mvvm.Views.Dialogs;

/// <summary>Default <see cref="IDialogService"/> backed by MessageBox dialogs and dockable/detachable Status &amp; Search panels.</summary>
public sealed class DialogService : IDialogService
{
	/// <inheritdoc/>
	public Task<DialogResult> ShowMessageAsync( string message )
		=> MessageBox.ShowAsync( message );

	/// <inheritdoc/>
	public Task<DialogResult> ShowMessageAsync( string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon = MessageBoxIcon.None )
		=> MessageBox.ShowAsync( message, caption, buttons, icon );

	/// <inheritdoc/>
	public async Task<bool> ConfirmAsync( string message, string caption, MessageBoxIcon icon = MessageBoxIcon.Question )
		=> await MessageBox.ShowAsync( message, caption, MessageBoxButtons.YesNoCancel, icon ) is DialogResult.Yes or DialogResult.OK;

	private Window? _statusWindow;
	private bool _reattachingStatus;
	private Window? _searchWindow;
	private bool _reattachingSearch;

	internal StatusDialogView? CurrentStatusPage { get; private set; }
	internal SearchOdooView? CurrentSearchPage { get; private set; }

	/// <inheritdoc/>
	public void ShowStatusWindow()
	{
		if( _statusWindow is { } open )
		{
			open.Activate();
			return;
		}
		if( HackApp.HomePage is { } home )
		{
			StatusDialogView page = CurrentStatusPage ?? new StatusDialogView();
			CurrentStatusPage = page;
			home.ShowStatusPanel( page );
			HackApp.Window?.Activate();
			return;
		}
		DetachStatusWindow();
	}

	/// <inheritdoc/>
	public void ToggleStatusWindow()
	{
		if( _statusWindow is { } open )
		{
			open.Activate();
			return;
		}
		if( HackApp.HomePage is { IsStatusDrawerOpen: true } )
			CloseStatusWindow();
		else
			ShowStatusWindow();
	}

	/// <inheritdoc/>
	public void CloseStatusWindow()
	{
		if( _statusWindow is { } win )
		{
			win.Close();
			return;
		}
		HackApp.HomePage?.HideStatusPanelImmediate();
	}

	/// <inheritdoc/>
	public void DetachStatusWindow()
	{
		if( _statusWindow is { } open )
		{
			open.Activate();
			return;
		}
		StatusDialogView page = HackApp.HomePage?.TakeStatusPageForDetach() ?? CurrentStatusPage ?? new StatusDialogView();
		CurrentStatusPage = page;
		Window window = WindowHelper.CreateWindowForExistingPage( page );
		page.ParentWindow = window;
		page.SetHostMode( isDocked: false );
		page.SetMinimalMode( UIStorage.WorkspaceLayout.StatusMinimalMode );
		if( UIStorage.WorkspaceLayout.StatusMinimalMode )
			ApplyDetachedWindowSize( window, "StatusDialogView", 920, 120 );
		_statusWindow = window;
		window.Closed += OnStatusWindowClosed;
	}

	/// <inheritdoc/>
	public void SetStatusMinimalMode( bool minimal )
	{
		UIStorage.WorkspaceLayout.StatusMinimalMode = minimal;
		SaveLayoutSafe();
		CurrentStatusPage?.SetMinimalMode( minimal );
		if( _statusWindow is { } win )
		{
			if( minimal )
				ApplyDetachedWindowSize( win, "StatusDialogView", 920, 120 );
			else
				WindowHelper.SetWindowConfig( win, WindowConfig.Preset( nameof( StatusDialogView ) ) );
		}
		else if( HackApp.HomePage is { IsStatusDrawerOpen: true } home )
		{
			home.ApplyStatusDrawerMode();
		}
	}

	/// <inheritdoc/>
	public void ShowFullStatusWindow()
	{
		SetStatusMinimalMode( false );
		ShowStatusWindow();
	}

	/// <inheritdoc/>
	public void DockStatusWindow()
	{
		if( _statusWindow is not { } win || CurrentStatusPage is not { } page )
		{
			ShowStatusWindow();
			return;
		}
		if( HackApp.HomePage is not { } home )
			return;

		_reattachingStatus = true;
		win.Closed -= OnStatusWindowClosed;
		DetachRootFromWindow( win, page );
		_statusWindow = null;
		win.Close();
		_reattachingStatus = false;

		home.ShowStatusPanel( page );
		HackApp.Window?.Activate();
	}

	private void OnStatusWindowClosed( object sender, WindowEventArgs args )
	{
		if( _reattachingStatus )
			return;
		if( CurrentStatusPage is not null )
			CurrentStatusPage.ParentWindow = null;
		CurrentStatusPage = null;
		_statusWindow = null;
	}

	/// <inheritdoc/>
	public void ShowSearchWindow()
	{
		if( _searchWindow is { } open )
		{
			open.Activate();
			return;
		}
		if( HackApp.HomePage is { } home )
		{
			SearchOdooView page = CurrentSearchPage ?? new SearchOdooView();
			CurrentSearchPage = page;
			home.ShowSearchPanel( page );
			HackApp.Window?.Activate();
			return;
		}
		DetachSearchWindow();
	}

	/// <inheritdoc/>
	public void ToggleSearchWindow()
	{
		if( _searchWindow is { } open )
		{
			open.Activate();
			return;
		}
		if( HackApp.HomePage is { IsSearchDrawerOpen: true } )
			CloseSearchWindow();
		else
			ShowSearchWindow();
	}

	/// <inheritdoc/>
	public void CloseSearchWindow()
	{
		if( _searchWindow is { } win )
		{
			win.Close();
			return;
		}
		HackApp.HomePage?.HideSearchPanelImmediate();
	}

	/// <inheritdoc/>
	public void DetachSearchWindow()
	{
		if( _searchWindow is { } open )
		{
			open.Activate();
			return;
		}
		SearchOdooView page = HackApp.HomePage?.TakeSearchPageForDetach() ?? CurrentSearchPage ?? new SearchOdooView();
		CurrentSearchPage = page;
		Window window = WindowHelper.CreateWindowForExistingPage( page );
		page.StoreWindowInstance( window );
		page.SetHostMode( isDocked: false );
		page.SetMinimalMode( UIStorage.WorkspaceLayout.SearchMinimalMode );
		if( UIStorage.WorkspaceLayout.SearchMinimalMode )
			ApplyDetachedWindowSize( window, "SearchOdooView", 980, 320 );
		_searchWindow = window;
		window.Closed += OnSearchWindowClosed;
	}

	/// <inheritdoc/>
	public void SetSearchMinimalMode( bool minimal )
	{
		UIStorage.WorkspaceLayout.SearchMinimalMode = minimal;
		SaveLayoutSafe();
		CurrentSearchPage?.SetMinimalMode( minimal );
		if( _searchWindow is { } win )
		{
			if( minimal )
				ApplyDetachedWindowSize( win, "SearchOdooView", 980, 320 );
			else
				WindowHelper.SetWindowConfig( win, WindowConfig.Preset( nameof( SearchOdooView ) ) );
		}
		else if( HackApp.HomePage is { IsSearchDrawerOpen: true } home )
		{
			home.ArrangeSearchDrawer();
		}
	}

	/// <inheritdoc/>
	public void ShowFullSearchWindow()
	{
		SetSearchMinimalMode( false );
		ShowSearchWindow();
	}

	private static void ApplyDetachedWindowSize( Window window, string presetKey, int width, int height )
	{
		var preset = WindowConfig.Preset( presetKey );
		WindowHelper.SetWindowConfig( window, new WindowConfig
		{
			Title = preset.Title,
			WindowKind = preset.WindowKind,
			PositionAndSize = ( 0, 0, width, height ),
		} );
	}

	private static void SaveLayoutSafe()
	{
		try
		{
			AppConfigStore.Save( UIStorage.GlobalData );
		}
		catch
		{
		}
	}

	/// <inheritdoc/>
	public void DockSearchWindow()
	{
		if( _searchWindow is not { } win || CurrentSearchPage is not { } page )
		{
			ShowSearchWindow();
			return;
		}
		if( HackApp.HomePage is not { } home )
			return;

		_reattachingSearch = true;
		win.Closed -= OnSearchWindowClosed;
		DetachRootFromWindow( win, page );
		_searchWindow = null;
		win.Close();
		_reattachingSearch = false;

		home.ShowSearchPanel( page );
		HackApp.Window?.Activate();
	}

	private void OnSearchWindowClosed( object sender, WindowEventArgs args )
	{
		if( _reattachingSearch )
			return;
		if( CurrentSearchPage is not null )
			CurrentSearchPage.StoreWindowInstance( null );
		CurrentSearchPage = null;
		_searchWindow = null;
	}

	private static void DetachRootFromWindow( Window window, Page page )
	{
		if( window.Content is Frame frame )
			frame.Content = null;
		window.Content = null;
		WindowHelper.UnregisterPageWindow( page );
	}
}
