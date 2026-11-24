using System;

using HackPDM.Common;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace HackPDM.Settings;

/// <summary>
/// Displays the Odoo server's registered <c>hp.type</c> extensions/categories and <c>hp.entry.name.filter</c> ignore rules.
/// </summary>
public sealed partial class OdooFileTypeManagerView : Page
{
	private WeakReference<Window>? _windowRef;

	public OdooFileTypeManagerViewModel ViewModel { get; } = new();

	public OdooFileTypeManagerView()
	{
		InitializeComponent();
		KeyDown += OnPageKeyDown;
		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
	}

	public void StoreWindowInstance( Window? window )
		=> _windowRef = window is null ? null : new WeakReference<Window>( window );

	private void OnLoaded( object sender, RoutedEventArgs e )
		=> ViewModel.LoadFromCache();

	private void OnUnloaded( object sender, RoutedEventArgs e )
	{
		ViewModel.CancelPendingOperations();
		_windowRef = null;
	}

	private void CloseButton_Click( object sender, RoutedEventArgs e ) => CloseHostWindow();

	private void OnPageKeyDown( object sender, KeyRoutedEventArgs e )
	{
		if( e.Key == Windows.System.VirtualKey.Escape )
		{
			e.Handled = true;
			CloseHostWindow();
		}
	}

	private void CloseHostWindow()
	{
		Window? target = this.Window;
		if( target is null && _windowRef is not null )
			_windowRef.TryGetTarget( out target );
		target?.Close();
	}
}
