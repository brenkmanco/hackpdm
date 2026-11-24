using System.Text.Json;

using HackPDM.Core.Graph;
using HackPDM.Core.Theming;

namespace HackPDM.Tests.Core;

public sealed class UiSettingsTests
{
	[Fact]
	public void WindowPresetKeys_MatchDefaultPresetWindowConfigEntries()
	{
		WindowSettingsData defaults = new();
		string[] expectedKeys =
		[
			WindowPresetKeys.ProfileManager,
			WindowPresetKeys.OdooSettings,
			WindowPresetKeys.OdooSettingsView,
			WindowPresetKeys.HackSettings,
			WindowPresetKeys.HackSettingsView,
			WindowPresetKeys.ConfigSettings,
			WindowPresetKeys.HackFileManager,
			WindowPresetKeys.DependencyGraph,
			WindowPresetKeys.NotLoggedIn,
			WindowPresetKeys.MessageBox,
			WindowPresetKeys.StatusDialogView,
			WindowPresetKeys.SearchOdooView,
			WindowPresetKeys.OdooFileTypeManagerView,
			WindowPresetKeys.ApplicationSettingsView,
			WindowPresetKeys.AppSettings,
		];

		foreach( string key in expectedKeys )
			Assert.True( defaults.PresetWindowConfig.ContainsKey( key ), $"Missing preset '{key}'" );

		Assert.Equal( expectedKeys.Length, defaults.PresetWindowConfig.Count );
		Assert.Equal( "OdooSettings", WindowPresetKeys.OdooSettings );
		Assert.Equal( "OdooSettingsView", WindowPresetKeys.OdooSettingsView );
		Assert.Equal( "HackSettings", WindowPresetKeys.HackSettings );
		Assert.Equal( "HackSettingsView", WindowPresetKeys.HackSettingsView );
	}

	[Fact]
	public void AppUiConfig_RoundTripsViaSourceGeneratedOptions()
	{
		AppUiConfig config = new();
		config.Theme.Palette = PaletteName.NavyBlue;
		config.Theme.Form = FormName.Fancy;
		config.Theme.Mode = AppThemeMode.Dark;
		config.Theme.Dark[ "AccentFillColorDefaultBrush" ] = "#FFF2B134";
		config.WorkspaceLayout.ApplyPreset( WorkspaceLayoutPreset.RightInspector );

		string json = JsonSerializer.Serialize( config, AppUiConfig.JsonOptions );
		AppUiConfig? parsed = JsonSerializer.Deserialize<AppUiConfig>( json, AppUiConfig.JsonOptions );

		Assert.NotNull( parsed );
		Assert.Equal( PaletteName.NavyBlue, parsed!.Theme.Palette );
		Assert.Equal( FormName.Fancy, parsed.Theme.Form );
		Assert.Equal( "#FFF2B134", parsed.Theme.Dark[ "AccentFillColorDefaultBrush" ] );
		Assert.Equal( DetailsDockPosition.Right, parsed.WorkspaceLayout.DetailsDock );
		Assert.Equal( PreviewDockPlacement.BeforeDetails, parsed.WorkspaceLayout.PreviewPlacement );
		Assert.Equal( config.EColors.OrangeEntry, parsed.EColors.OrangeEntry );
		Assert.Equal( config.Win.PresetWindowConfig[ WindowPresetKeys.HackFileManager ].PositionAndSize,
			parsed.Win.PresetWindowConfig[ WindowPresetKeys.HackFileManager ].PositionAndSize );
	}

	[Fact]
	public void OutOfRangeEnumNumbersAndUnknownNames_FallBackSafelyOnDeserialization()
	{
		const string json = """
		{
			"Theme": {
				"Mode": 99,
				"Palette": 99,
				"Form": 99
			},
			"ColumnSettings": {
				"GroupBy": { "FieldName": "Status", "SortDirection": 99 }
			},
			"WindowSettings": {
				"Windows": {
					"Custom": { "Title": "X", "WindowKind": 99 }
				}
			},
			"WorkspaceLayout": {
				"FoldersDock": 99,
				"DetailsDock": "NotAValue",
				"PreviewPlacement": "99",
				"SearchDock": 99
			}
		}
		""";

		AppUiConfig parsed = JsonSerializer.Deserialize<AppUiConfig>( json, AppUiConfig.JsonOptions )!;

		Assert.Equal( AppThemeMode.Dark, parsed.Theme.Mode );
		Assert.Null( parsed.Theme.Palette );
		Assert.Equal( FormName.Default, parsed.Theme.Form );
		Assert.Equal( EntrySortDirection.Ascending, parsed.ColumnsSettings.GroupBy.SortDirection );
		Assert.Equal( WindowKind.Overlapped, parsed.Win.PresetWindowConfig[ "Custom" ].WindowKind );
		Assert.Equal( FolderDockSide.Left, parsed.WorkspaceLayout.FoldersDock );
		Assert.Equal( DetailsDockPosition.Bottom, parsed.WorkspaceLayout.DetailsDock );
		Assert.Equal( PreviewDockPlacement.AfterDetails, parsed.WorkspaceLayout.PreviewPlacement );
		Assert.Equal( FolderDockSide.Left, parsed.WorkspaceLayout.SearchDock );

		// Startup preset resolution must succeed without throwing
		AppPalette appPalette = ThemePresets.Palette( parsed.Theme.EffectivePalette );
		AppForm appForm = ThemePresets.Form( parsed.Theme.Form );
		GraphTheme graphTheme = GraphThemes.From( parsed.Theme.EffectivePalette, parsed.Theme.Form );
		Assert.NotNull( appPalette );
		Assert.NotNull( appForm );
		Assert.NotNull( graphTheme );
	}

	[Fact]
	public void ExplicitJsonNulls_CoalesceToNonNullDefaults()
	{
		const string json = """
		{
			"WindowSettings": null,
			"OdooEntryColors": null,
			"ColumnSettings": null,
			"Theme": {
				"Light": null,
				"Dark": null
			},
			"WorkspaceLayout": null
		}
		""";

		AppUiConfig parsed = JsonSerializer.Deserialize<AppUiConfig>( json, AppUiConfig.JsonOptions )!;

		Assert.NotNull( parsed.Win );
		Assert.NotNull( parsed.Win.PresetWindowConfig );
		Assert.NotEmpty( parsed.Win.PresetWindowConfig );
		Assert.NotNull( parsed.EColors );
		Assert.NotNull( parsed.ColumnsSettings );
		Assert.NotNull( parsed.ColumnsSettings.Columns );
		Assert.NotNull( parsed.ColumnsSettings.GroupBy );
		Assert.NotNull( parsed.Theme );
		Assert.NotNull( parsed.Theme.Light );
		Assert.NotNull( parsed.Theme.Dark );
		Assert.NotNull( parsed.WorkspaceLayout );
	}

	[Fact]
	public void ProgrammaticOutOfRangeEnums_CoalesceInThemeSettingsSetters()
	{
		ThemeSettings ts = new()
		{
			Mode = (AppThemeMode)123,
			Palette = (PaletteName)123,
			Form = (FormName)123,
			Light = null!,
			Dark = null!,
		};

		Assert.Equal( AppThemeMode.Dark, ts.Mode );
		Assert.Null( ts.Palette );
		Assert.Equal( FormName.Default, ts.Form );
		Assert.Equal( PaletteName.Dark, ts.EffectivePalette );
		Assert.NotNull( ts.Light );
		Assert.NotNull( ts.Dark );
	}

	[Fact]
	public void WorkspaceLayoutPresets_UpdateDockingAndDimensions()
	{
		WorkspaceLayoutSettings layout = new();
		layout.ApplyPreset( WorkspaceLayoutPreset.FocusEntries );
		Assert.False( layout.ShowFoldersPane );
		Assert.False( layout.ShowPreviewPane );
		Assert.Equal( 220, layout.DetailsPaneHeight );

		layout.ApplyPreset( WorkspaceLayoutPreset.RightSidebar );
		Assert.True( layout.ShowFoldersPane );
		Assert.Equal( FolderDockSide.Right, layout.FoldersDock );

		layout.ApplyPreset( WorkspaceLayoutPreset.Balanced );
		Assert.Equal( FolderDockSide.Left, layout.FoldersDock );
		Assert.Equal( DetailsDockPosition.Bottom, layout.DetailsDock );
	}
}
