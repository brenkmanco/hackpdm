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
			WindowPresetKeys.MainWindow,
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
			WindowPresetKeys.StatusDialogViewMinimal,
			WindowPresetKeys.SearchOdooView,
			WindowPresetKeys.SearchOdooViewMinimal,
			WindowPresetKeys.OdooFileTypeManagerView,
			WindowPresetKeys.ApplicationSettingsView,
			WindowPresetKeys.AppSettings,
			WindowPresetKeys.PipWindow,
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

	[Fact]
	public void SanitizePresets_RepairsLegacyZeroDimensionsAndAddsMissingDefaults()
	{
		const string json = """
		{
			"WindowSettings": {
				"Windows": {
					"ProfileManager": { "Title": "Profile Manager", "PositionAndSize": "(0, 0, 0, 0)", "WindowKind": "Overlapped" },
					"ConfigSettings": { "Title": "Configuration Settings", "PositionAndSize": "(0, 0, 0, 0)", "WindowKind": "Overlapped" },
					"HackFileManager": { "Title": "Hack File Manager", "PositionAndSize": "(0, 0, 0, 0)", "WindowKind": "Overlapped" },
					"NotLoggedIn": { "Title": "Not Logged In", "PositionAndSize": "(0, 0, 0, 0)", "WindowKind": "Overlapped" },
					"MessageBox": { "Title": "Info", "PositionAndSize": "(0, 0, 0, 0)", "WindowKind": "CompactOverlay" },
					"DynamicJsonForm": { "Title": "Json Editor", "PositionAndSize": "(0, 0, 0, 0)", "WindowKind": "Overlapped" }
				}
			}
		}
		""";

		AppUiConfig parsed = JsonSerializer.Deserialize<AppUiConfig>( json, AppUiConfig.JsonOptions )!;
		var windows = parsed.Win.PresetWindowConfig;

		Assert.Equal( 960, windows[ WindowPresetKeys.ProfileManager ].PositionAndSize.z );
		Assert.Equal( 660, windows[ WindowPresetKeys.ProfileManager ].PositionAndSize.w );
		Assert.Equal( 1480, windows[ WindowPresetKeys.HackFileManager ].PositionAndSize.z );
		Assert.Equal( 880, windows[ WindowPresetKeys.HackFileManager ].PositionAndSize.w );
		Assert.Equal( 520, windows[ WindowPresetKeys.MessageBox ].PositionAndSize.z );
		Assert.Equal( 280, windows[ WindowPresetKeys.MessageBox ].PositionAndSize.w );
		Assert.Equal( WindowKind.Overlapped, windows[ WindowPresetKeys.MessageBox ].WindowKind );
		Assert.False( windows[ WindowPresetKeys.HackFileManager ].HasSavedSize );
		Assert.False( windows[ WindowPresetKeys.HackFileManager ].HasSavedPosition );
		Assert.False( windows[ WindowPresetKeys.HackFileManager ].IsMaximized );

		Assert.Equal( 900, windows[ "DynamicJsonForm" ].PositionAndSize.z );
		Assert.Equal( 620, windows[ "DynamicJsonForm" ].PositionAndSize.w );

		Assert.True( windows.ContainsKey( WindowPresetKeys.MainWindow ) );
		Assert.True( windows.ContainsKey( WindowPresetKeys.DependencyGraph ) );
		Assert.True( windows.ContainsKey( WindowPresetKeys.AppSettings ) );
		Assert.True( windows.ContainsKey( WindowPresetKeys.PipWindow ) );
		Assert.True( windows.ContainsKey( WindowPresetKeys.StatusDialogViewMinimal ) );
		Assert.True( windows.ContainsKey( WindowPresetKeys.SearchOdooViewMinimal ) );
	}

	[Fact]
	public void WindowConfigData_NegativeMultiMonitorCoordinatesAndState_RoundTrip()
	{
		AppUiConfig config = new();
		WindowConfigData main = config.Win.PresetWindowConfig[ WindowPresetKeys.MainWindow ];
		main.PositionAndSize = ( -1920, -349, 1600, 950 );
		main.HasSavedPosition = true;
		main.HasSavedSize = true;
		main.IsMaximized = true;

		string json = JsonSerializer.Serialize( config, AppUiConfig.JsonOptions );
		AppUiConfig parsed = JsonSerializer.Deserialize<AppUiConfig>( json, AppUiConfig.JsonOptions )!;
		WindowConfigData restored = parsed.Win.PresetWindowConfig[ WindowPresetKeys.MainWindow ];

		Assert.Equal( -1920, restored.PositionAndSize.x );
		Assert.Equal( -349, restored.PositionAndSize.y );
		Assert.Equal( 1600, restored.PositionAndSize.z );
		Assert.Equal( 950, restored.PositionAndSize.w );
		Assert.True( restored.HasSavedPosition );
		Assert.True( restored.HasSavedSize );
		Assert.True( restored.IsMaximized );
	}

	[Fact]
	public void WorkspaceLayoutSettings_GraphTogglesAndStatusSplits_ClampAndRoundTrip()
	{
		WorkspaceLayoutSettings defaults = new();
		Assert.True( defaults.ShowGraphFoldersPane );
		Assert.True( defaults.ShowGraphFilesPane );
		Assert.Equal( 0.6, defaults.StatusTopSplitRatio );
		Assert.Equal( 0.5, defaults.StatusLeftSplitRatio );

		defaults.StatusTopSplitRatio = 0.01;
		Assert.Equal( 0.2, defaults.StatusTopSplitRatio );
		defaults.StatusTopSplitRatio = 0.99;
		Assert.Equal( 0.8, defaults.StatusTopSplitRatio );
		defaults.StatusTopSplitRatio = double.NaN;
		Assert.Equal( 0.6, defaults.StatusTopSplitRatio );

		defaults.StatusLeftSplitRatio = double.PositiveInfinity;
		Assert.Equal( 0.5, defaults.StatusLeftSplitRatio );

		AppUiConfig config = new();
		config.WorkspaceLayout.ShowGraphFoldersPane = false;
		config.WorkspaceLayout.ShowGraphFilesPane = false;
		config.WorkspaceLayout.StatusTopSplitRatio = 0.72;
		config.WorkspaceLayout.StatusLeftSplitRatio = 0.38;

		string json = JsonSerializer.Serialize( config, AppUiConfig.JsonOptions );
		AppUiConfig parsed = JsonSerializer.Deserialize<AppUiConfig>( json, AppUiConfig.JsonOptions )!;

		Assert.False( parsed.WorkspaceLayout.ShowGraphFoldersPane );
		Assert.False( parsed.WorkspaceLayout.ShowGraphFilesPane );
		Assert.Equal( 0.72, parsed.WorkspaceLayout.StatusTopSplitRatio, 3 );
		Assert.Equal( 0.38, parsed.WorkspaceLayout.StatusLeftSplitRatio, 3 );
	}
}
