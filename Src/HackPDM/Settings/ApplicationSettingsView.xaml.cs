using System;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Themes;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace HackPDM.Settings;

/// <summary>Application settings hub (the NavigationView's settings gear): one section per settings page.</summary>
public sealed partial class ApplicationSettingsView : Page
{
	public const string ThemeSectionKey = "Theme";
	public const string StatusSectionKey = "Status";
	public const string AboutSectionKey = "About";

	public ApplicationSettingsView()
	{
		InitializeComponent();
		Loaded += ( _, _ ) => ShowSection( SettingsSections.SelectedItem );
	}

	private void SettingsSections_SelectionChanged( SelectorBar sender, SelectorBarSelectionChangedEventArgs args )
		=> ShowSection( sender.SelectedItem );

	/// <summary>Switches the active tab (for example <see cref="StatusSectionKey"/>, <see cref="ThemeSectionKey"/>, or <see cref="AboutSectionKey"/>).</summary>
	public void SelectSection( string sectionName )
	{
		SelectorBarItem target = sectionName switch
		{
			StatusSectionKey => StatusSection,
			AboutSectionKey => AboutSection,
			_ => ThemeSection,
		};
		SettingsSections.SelectedItem = target;
		ShowSection( target );
	}

	private void ShowSection( SelectorBarItem? item )
	{
		Type? page = item?.Name switch
		{
			nameof( ThemeSection ) => typeof( ThemeEditorView ),
			nameof( StatusSection ) => typeof( StatusSettingsView ),
			nameof( AboutSection ) => typeof( AboutUpdatesView ),
			_ => null,
		};
		if( page is null || SectionFrame.CurrentSourcePageType == page )
			return;
		try
		{
			bool firstLoad = SectionFrame.CurrentSourcePageType is null;
			int oldIndex = SectionOrder( SectionFrame.CurrentSourcePageType );
			int newIndex = SectionOrder( page );
			NavigationSlideAxis slide = firstLoad
				? NavigationSlideAxis.None
				: newIndex > oldIndex
					? NavigationSlideAxis.Left
					: NavigationSlideAxis.Right;

			SectionFrame.Navigate( page, null, new SuppressNavigationTransitionInfo() );
			NavigationTransitions.Play( SectionFrame.Content, slide );
		}
		catch( Exception e )
		{
			HackLogger.Error( "Settings", $"Settings page {page.Name} failed to open: {e.Message}", e );
			SectionFrame.Content = new TextBlock { Text = $"Could not open {item?.Text}: {e.Message}", Margin = new( 12 ) };
		}
	}

	private static int SectionOrder( Type? page ) => page == typeof( ThemeEditorView ) ? 0 : page == typeof( StatusSettingsView ) ? 1 : 2;
}
