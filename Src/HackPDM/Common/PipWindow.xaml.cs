using System;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace HackPDM.Common;

/// <summary>
/// Compact always-on-top Picture-in-Picture window for the file manager's image preview.
/// Uses <see cref="AppWindowPresenterKind.CompactOverlay"/> on Windows when available, and falls back
/// to an always-on-top <see cref="OverlappedPresenter"/> on platforms (such as Uno Skia Desktop) where
/// CompactOverlay is unsupported.
/// </summary>
public sealed partial class PipWindow : Window
{
	public event EventHandler? ReturnRequested;

	public PipWindow()
	{
		InitializeComponent();
		ConfigureWindow();
	}

	private void ConfigureWindow()
	{
		bool compactOverlay = false;
#if WINDOWS
		if( this.SetWindowType( AppWindowPresenterKind.CompactOverlay ) )
		{
			compactOverlay = true;
			ExtendsContentIntoTitleBar = true;
			PipTitleBar.Visibility = Visibility.Visible;
			SetTitleBar( PipTitleBar );
		}
#endif
		if( !compactOverlay )
		{
			try
			{
				this.SetWindowType( AppWindowPresenterKind.Overlapped );
				if( AppWindow?.Presenter is OverlappedPresenter presenter )
				{
					presenter.IsAlwaysOnTop = true;
					presenter.IsMaximizable = false;
					presenter.IsMinimizable = false;
				}
			}
			catch
			{
				// Some window managers restrict presenter flags; leave defaults intact
			}
		}

		try
		{
			WindowPreset preset = WindowHelper.Preset( WindowHelper.Presets.PipWindow );
			WindowHelper.ApplyPreset(
				this,
				preset with
				{
					Title = string.IsNullOrWhiteSpace( Title ) ? preset.Title : Title,
					Kind = compactOverlay ? AppWindowPresenterKind.CompactOverlay : AppWindowPresenterKind.Overlapped,
					IsMaximized = false,
				} );
			WindowHelper.TrackWindow( this, WindowHelper.Presets.PipWindow );
		}
		catch
		{
			// Non-fatal in headless/restricted environments
		}

		WindowHelper.ApplyAppIcon( this );
		AppComposition.Theme.Attach( this );
	}

	/// <summary>Updates the preview image and fallback message displayed in the PiP window.</summary>
	public void SetPreview( ImageSource? source, string? statusText = null, string? title = null )
	{
		if( !string.IsNullOrWhiteSpace( title ) )
			Title = title;
		PipImage.Source = source;
		bool hasImage = source is not null;
		PipImage.Visibility = hasImage ? Visibility.Visible : Visibility.Collapsed;
		PipEmptyText.Visibility = hasImage ? Visibility.Collapsed : Visibility.Visible;
		PipEmptyText.Text = string.IsNullOrWhiteSpace( statusText ) ? "No preview available" : statusText;
	}

	private void OnExitPipClicked( object sender, RoutedEventArgs e )
		=> ReturnRequested?.Invoke( this, EventArgs.Empty );
}
