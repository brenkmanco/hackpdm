using System;

using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Mvvm.Views.Settings;

/// <summary>Application settings hub (the NavigationView's settings gear): one section per settings page.</summary>
public sealed partial class ApplicationSettingsView : Page
{
	public ApplicationSettingsView()
	{
		InitializeComponent();
		Loaded += ( _, _ ) => ShowSection( SettingsSections.SelectedItem );
	}

	private void SettingsSections_SelectionChanged( SelectorBar sender, SelectorBarSelectionChangedEventArgs args )
		=> ShowSection( sender.SelectedItem );

	private void ShowSection( SelectorBarItem? item )
	{
		Type? page = item?.Name switch
		{
			nameof( ThemeSection ) => typeof( ThemeEditorView ),
			_ => null,
		};
		if( page is null || SectionFrame.CurrentSourcePageType == page )
			return;
		try
		{
			SectionFrame.Navigate( page );
		}
		catch( Exception e )
		{
			Console.Error.WriteLine( $"settings page {page.Name} failed to open: {e}" );
			SectionFrame.Content = new TextBlock { Text = $"Could not open {item?.Text}: {e.Message}", Margin = new( 12 ) };
		}
	}
}
