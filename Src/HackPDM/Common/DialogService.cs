using System;
using System.Threading.Tasks;

using HackPDM.Core.Configuration;
using HackPDM.Core.Pdm;
using HackPDM.Search;
using HackPDM.Shell;
using HackPDM.Status;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace HackPDM.Common;

public interface IDialogService
{
	Task<DialogResult> ShowMessageAsync(
		string message,
		string? caption = null,
		MessageBoxButtons buttons = MessageBoxButtons.OK,
		MessageBoxIcon icon = MessageBoxIcon.Information );

	Task<bool> ConfirmAsync( string message, string caption, MessageBoxIcon icon = MessageBoxIcon.Question );

	void ShowStatusWindow();
	void ToggleStatusWindow();
	void CloseStatusWindow();
	void DetachStatusWindow();
	void DockStatusWindow();
	void SetStatusMinimalMode( bool minimal );
	void ShowFullStatusWindow();

	void ShowSearchWindow();
	void ToggleSearchWindow();
	void CloseSearchWindow();
	void DetachSearchWindow();
	void DockSearchWindow();
	void SetSearchMinimalMode( bool minimal );
	void ShowFullSearchWindow();
}

/// <summary>Opens message boxes and manages dockable/detachable Status Log and Search Vault panels.</summary>
public sealed class DialogService : IDialogService
{
	private readonly DockablePanelController<StatusDialogView> _status;
	private readonly DockablePanelController<SearchOdooView> _search;

	public DialogService()
	{
		_status = new(
			presetKey: WindowHelper.Presets.StatusDialogView,
			minimalPresetKey: WindowHelper.Presets.StatusDialogViewMinimal,
#if WINDOWS
			minimalSize: ( 920, 170 ),
#else
			minimalSize: ( 920, 420 ),
#endif
			createPage: () => new StatusDialogView(),
			getMinimal: () => UIStorage.WorkspaceLayout.StatusMinimalMode,
			setMinimal: v => UIStorage.WorkspaceLayout.StatusMinimalMode = v,
			isDrawerOpen: h => h.IsStatusDrawerOpen,
			showDocked: ( h, p ) => h.ShowStatusPanel( p ),
			hideDocked: h => h.HideStatusPanelImmediate(),
			takeForDetach: h => h.TakeStatusPageForDetach(),
			refreshDocked: h => h.ApplyStatusDrawerMode() );

		_search = new(
			presetKey: WindowHelper.Presets.SearchOdooView,
			minimalPresetKey: WindowHelper.Presets.SearchOdooViewMinimal,
			minimalSize: ( 980, 320 ),
			createPage: () => new SearchOdooView(),
			getMinimal: () => UIStorage.WorkspaceLayout.SearchMinimalMode,
			setMinimal: v => UIStorage.WorkspaceLayout.SearchMinimalMode = v,
			isDrawerOpen: h => h.IsSearchDrawerOpen,
			showDocked: ( h, p ) => h.ShowSearchPanel( p ),
			hideDocked: h => h.HideSearchPanelImmediate(),
			takeForDetach: h => h.TakeSearchPageForDetach(),
			refreshDocked: h => h.ArrangeSearchDrawer(),
			canOpen: () => !AppComposition.Session.IsVersionMismatch );
	}

	internal StatusDialogView? CurrentStatusPage => _status.CurrentPage;
	internal SearchOdooView? CurrentSearchPage => _search.CurrentPage;

	public Task<DialogResult> ShowMessageAsync(
		string message,
		string? caption = null,
		MessageBoxButtons buttons = MessageBoxButtons.OK,
		MessageBoxIcon icon = MessageBoxIcon.Information )
		=> MessageBox.ShowAsync( message, caption, buttons, icon );

	public async Task<bool> ConfirmAsync( string message, string caption, MessageBoxIcon icon = MessageBoxIcon.Question )
		=> await MessageBox.ShowAsync( message, caption, MessageBoxButtons.YesNoCancel, icon ) is DialogResult.Yes or DialogResult.OK;

	public void ShowStatusWindow() => UiDispatcher.RunOrPost( _status.Show );
	public void ToggleStatusWindow() => UiDispatcher.RunOrPost( _status.Toggle );
	public void CloseStatusWindow() => UiDispatcher.RunOrPost( _status.Close );
	public void DetachStatusWindow() => UiDispatcher.RunOrPost( _status.Detach );
	public void DockStatusWindow() => UiDispatcher.RunOrPost( _status.Dock );
	public void SetStatusMinimalMode( bool minimal ) => UiDispatcher.RunOrPost( () => _status.SetMinimal( minimal ) );
	public void ShowFullStatusWindow() => UiDispatcher.RunOrPost( () => { _status.SetMinimal( false ); _status.Show(); } );

	public void ShowSearchWindow() => UiDispatcher.RunOrPost( _search.Show );
	public void ToggleSearchWindow() => UiDispatcher.RunOrPost( _search.Toggle );
	public void CloseSearchWindow() => UiDispatcher.RunOrPost( _search.Close );
	public void DetachSearchWindow() => UiDispatcher.RunOrPost( _search.Detach );
	public void DockSearchWindow() => UiDispatcher.RunOrPost( _search.Dock );
	public void SetSearchMinimalMode( bool minimal ) => UiDispatcher.RunOrPost( () => _search.SetMinimal( minimal ) );
	public void ShowFullSearchWindow() => UiDispatcher.RunOrPost( () => { _search.SetMinimal( false ); _search.Show(); } );

	private sealed class DockablePanelController<TPage>(
		string presetKey,
		string minimalPresetKey,
		(int Width, int Height) minimalSize,
		Func<TPage> createPage,
		Func<bool> getMinimal,
		Action<bool> setMinimal,
		Func<HomeView, bool> isDrawerOpen,
		Action<HomeView, TPage> showDocked,
		Action<HomeView> hideDocked,
		Func<HomeView, TPage?> takeForDetach,
		Action<HomeView> refreshDocked,
		Func<bool>? canOpen = null ) where TPage : Page, IDockablePanel
	{
		private Window? _window;

		public TPage? CurrentPage { get; private set; }

		public void Show()
		{
			if( canOpen?.Invoke() == false )
				return;
			if( _window is { } open )
			{
				open.Activate();
				return;
			}
			if( HackApp.HomePage is { } home )
			{
				TPage page = CurrentPage ?? createPage();
				CurrentPage = page;
				showDocked( home, page );
				HackApp.Window?.Activate();
				return;
			}
			Detach();
		}

		public void Toggle()
		{
			if( _window is { } open )
			{
				open.Activate();
				return;
			}
			if( HackApp.HomePage is { } home && isDrawerOpen( home ) )
				Close();
			else
				Show();
		}

		public void Close()
		{
			if( _window is { } win )
			{
				win.Close();
				return;
			}
			if( HackApp.HomePage is { } home )
				hideDocked( home );
		}

		public void Detach()
		{
			if( canOpen?.Invoke() == false )
				return;
			if( _window is { } open )
			{
				open.Activate();
				return;
			}
			TPage page = ( HackApp.HomePage is { } home ? takeForDetach( home ) : null ) ?? CurrentPage ?? createPage();
			CloseOpenPopups( page.XamlRoot );
			CurrentPage = page;
			bool minimal = getMinimal();
			string activeKey = minimal ? minimalPresetKey : presetKey;
			Window window = WindowHelper.CreateWindowForExistingPage( page, presetName: activeKey );
			page.HostWindow = window;
			page.SetHostMode( isDocked: false );
			ConfigureDetachedPopups( page );
			page.SetMinimalMode( minimal );
			if( minimal )
				ApplyMinimalSize( window, activeKey );
			_window = window;
			window.Closed += OnClosed;
		}

		public void SetMinimal( bool minimal )
		{
			setMinimal( minimal );
			UIStorage.SaveSafe();
			CurrentPage?.SetMinimalMode( minimal );
			if( _window is { } win )
			{
				string targetKey = minimal ? minimalPresetKey : presetKey;
				WindowHelper.RetargetTrackedWindow( win, targetKey );
				if( minimal )
					ApplyMinimalSize( win, targetKey );
				else
					WindowHelper.ApplyPreset( win, WindowHelper.Preset( targetKey ) );
			}
			else if( HackApp.HomePage is { } home && isDrawerOpen( home ) )
			{
				refreshDocked( home );
			}
		}

		public void Dock()
		{
			if( _window is not { } win || CurrentPage is not { } page )
			{
				Show();
				return;
			}
			if( HackApp.HomePage is not { } home )
				return;

			CloseOpenPopups( page.XamlRoot );
			win.Closed -= OnClosed;
			page.HostWindow = null;
			page.SetHostMode( isDocked: true );
			if( win.Content is Frame frame )
				frame.Content = null;
			win.Content = null;
			WindowHelper.UnregisterPageWindow( page );
			_window = null;
			win.Close();

			showDocked( home, page );
			HackApp.Window?.Activate();
		}

		private static void CloseOpenPopups( XamlRoot? root )
		{
			if( root is null )
				return;
			try
			{
				foreach( Popup popup in VisualTreeHelper.GetOpenPopupsForXamlRoot( root ) )
					popup.IsOpen = false;
			}
			catch
			{
			}
		}

		private static void ConfigureDetachedPopups( Page page )
		{
#if WINDOWS
			if( page.Content is not Panel root )
				return;
			for( int i = 0; i < root.Children.Count; i++ )
			{
				if( root.Children[ i ] is Popup popup && !popup.IsOpen )
					popup.ShouldConstrainToRootBounds = false;
			}
#else
			_ = page;
#endif
		}

		private void OnClosed( object sender, WindowEventArgs args )
		{
			if( sender is Window win )
				win.Closed -= OnClosed;
			if( CurrentPage is not null )
				CurrentPage.HostWindow = null;
			CurrentPage = null;
			_window = null;
		}

		private void ApplyMinimalSize( Window window, string key )
		{
			WindowPreset preset = WindowHelper.Preset( key );
			WindowPreset target = preset.HasSavedSize
				? preset
				: preset with
				{
					Width = Math.Max( preset.Width, minimalSize.Width ),
					Height = Math.Max( preset.Height, minimalSize.Height ),
				};
			WindowHelper.ApplyPreset( window, target, forceRestoreIfMaximized: true );
		}
	}
}
