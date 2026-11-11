using System;

using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.ViewModels.Settings;
using HackPDM.Shell;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Mvvm.Views.Settings;

/// <summary>Theme editor plus a preview of every control type the app uses.</summary>
public sealed partial class ThemeEditorView : Page
{
	public ThemeEditorViewModel ViewModel { get; } = new( AppComposition.Theme );

	// true while the view model is driving the picker, so its ColorChanged isn't sent back as an edit
	private bool _syncingPicker;

	public ThemeEditorView()
	{
		InitializeComponent();
		ViewModel.SelectedColorValueChanged += ShowInPicker;
		Unloaded += ( _, _ ) => ViewModel.SelectedColorValueChanged -= ShowInPicker;
		ShowInPicker( ArgbColor.TryParse( ViewModel.SelectedColor?.Hex, out var c ) ? c : null );
		FillSamples();
	}

	private void ShowInPicker( ArgbColor? color )
	{
		_syncingPicker = true;
		try
		{
			if( color is { } c )
				Picker.Color = Windows.UI.Color.FromArgb( c.A, c.R, c.G, c.B );
		}
		finally
		{
			_syncingPicker = false;
		}
	}

	private void Picker_ColorChanged( ColorPicker sender, ColorChangedEventArgs args )
	{
		if( _syncingPicker )
			return;
		var c = args.NewColor;
		ViewModel.SetSelectedColor( new( c.A, c.R, c.G, c.B ) );
	}

	private async void ShowDialog_Click( object sender, RoutedEventArgs e )
	{
		var dialog = new ContentDialog
		{
			XamlRoot = XamlRoot,
			Title = "Dialog",
			Content = "Message boxes and confirmations use this surface.",
			PrimaryButtonText = "OK",
			CloseButtonText = "Cancel",
			DefaultButton = ContentDialogButton.Primary,
			RequestedTheme = ActualTheme,
		};
		await dialog.ShowAsync();
	}

	#region Sample data for the preview

	public sealed class SampleRow( string name, string status, string size, string modUser )
	{
		public string Name { get; } = name;
		public string Status { get; } = status;
		public string Size { get; } = size;
		public string ModUser { get; } = modUser;
	}

	private void FillSamples()
	{
		SampleTable.ItemsSource = new[]
		{
			new SampleRow( "bracket.sldprt", "ok", "1.2 MB", "admin" ),
			new SampleRow( "housing.sldasm", "checked out", "4.8 MB", "jdoe" ),
			new SampleRow( "drawing.slddrw", "local only", "860 KB", "" ),
			new SampleRow( "spec.pdf", "remote only", "210 KB", "admin" ),
		};
		SampleList.ItemsSource = new[] { "List item", "Selected list item", "Another item" };
		SampleList.SelectedIndex = 1;

		SampleBreadcrumb.ItemsSource = new[] { "root", "projects", "widget", "parts" };

		var root = new TreeViewNode { Content = "root", IsExpanded = true };
		var projects = new TreeViewNode { Content = "projects", IsExpanded = true };
		projects.Children.Add( new TreeViewNode { Content = "widget" } );
		projects.Children.Add( new TreeViewNode { Content = "gadget" } );
		root.Children.Add( projects );
		root.Children.Add( new TreeViewNode { Content = "library" } );
		SampleTree.RootNodes.Add( root );
		// (RichEditBox.Document isn't implemented in Uno, so the rich edit sample starts empty)
	}

	#endregion
}
