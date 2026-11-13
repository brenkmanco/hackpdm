using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Core.Theming;
using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.ViewModels.Common;

namespace HackPDM.Mvvm.ViewModels.Settings;

/// <summary>One editable theme color in the editor list.</summary>
/// <param name="key">The color's catalog entry.</param>
/// <param name="isGroupStart">Whether this is the first color of its group.</param>
public partial class ThemeColorItem( ThemeColorKey key, bool isGroupStart ) : ObservableObject
{
	public ThemeColorKey Definition { get; } = key;
	public string Key => Definition.Key;
	public string Label => Definition.Label;
	public string Group => Definition.Group;
	public string Description => Definition.Description;

	/// <summary>First item of its group: the list shows the group name above it.</summary>
	public bool IsGroupStart { get; } = isGroupStart;

	/// <summary>Current color as "#AARRGGBB"; empty when the platform doesn't define this key.</summary>
	[ObservableProperty] public partial string Hex { get; set; } = "";

	/// <summary>Whether the color differs from the built-in theme.</summary>
	[ObservableProperty] public partial bool IsOverridden { get; set; }
}

/// <summary>
/// Theme editor: pick light/dark/system, change the shared theme colors (applied live), and save
/// them to AppConfig.json, which is loaded automatically at startup.
/// </summary>
public partial class ThemeEditorViewModel : ObservableObject
{
	private readonly IThemeService _theme;

	/// <summary>Builds the color list from <see cref="ThemeColorCatalog.All"/> and reads the current colors and mode.</summary>
	/// <param name="theme">Applies, edits and saves the theme.</param>
	public ThemeEditorViewModel( IThemeService theme )
	{
		_theme = theme;
		string? previousGroup = null;
		foreach( var key in ThemeColorCatalog.All )
		{
			Colors.Add( new( key, key.Group != previousGroup ) );
			previousGroup = key.Group;
		}
		SelectedPaletteIndex = PaletteIndex( _theme.Palette );
		SelectedFormIndex = FormIndex( _theme.Form );
		Refresh();
		SelectedColor = Colors.FirstOrDefault();
	}

	/// <summary>The color presets, in menu order.</summary>
	public string[] PaletteTitles { get; } = [ .. ThemePresets.Palettes.Select( p => p.Title ) ];

	/// <summary>The shape presets, in menu order.</summary>
	public string[] FormTitles { get; } = [ .. ThemePresets.Forms.Select( f => f.Title ) ];

	private static int PaletteIndex( PaletteName name ) => ThemePresets.Palettes.ToList().FindIndex( p => p.Name == name );
	private static int FormIndex( FormName name ) => ThemePresets.Forms.ToList().FindIndex( f => f.Name == name );

	public ObservableCollection<ThemeColorItem> Colors { get; } = [];

	public string SavePath => _theme.SavePath;

	/// <summary>Raised when the selected color's value changes outside the color picker (selection, reset, mode switch).</summary>
	public event Action<ArgbColor?>? SelectedColorValueChanged;

	[ObservableProperty] public partial int SelectedPaletteIndex { get; set; } = -1;

	[ObservableProperty] public partial int SelectedFormIndex { get; set; } = -1;

	/// <summary>What the selected style looks like.</summary>
	[ObservableProperty] public partial string FormDescription { get; set; } = "";

	/// <summary>A restart is needed to finish a palette or style switch.</summary>
	[ObservableProperty] public partial bool ShowRestartNotice { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor( nameof( ResetSelectedCommand ) )]
	public partial ThemeColorItem? SelectedColor { get; set; }

	/// <summary>Hex text box for the selected color.</summary>
	[ObservableProperty] public partial string HexInput { get; set; } = "";

	[ObservableProperty] public partial string EditingThemeText { get; set; } = "";

	/// <summary>Colors can be changed: false after switching the base mode, until a restart.</summary>
	[ObservableProperty]
	[NotifyCanExecuteChangedFor( nameof( ResetSelectedCommand ), nameof( ResetAllCommand ) )]
	public partial bool IsEditable { get; set; } = true;

	[ObservableProperty] public partial string RestartNotice { get; set; } = "";

	[ObservableProperty] public partial string StatusText { get; set; } = "";

	[ObservableProperty]
	[NotifyCanExecuteChangedFor( nameof( SaveCommand ), nameof( RevertCommand ) )]
	public partial bool HasUnsavedChanges { get; set; }

	partial void OnSelectedPaletteIndexChanged( int value )
	{
		if( value < 0 || value >= ThemePresets.Palettes.Count || ThemePresets.Palettes[ value ].Name == _theme.Palette )
			return;
		_theme.SetPalette( ThemePresets.Palettes[ value ].Name );
		HasUnsavedChanges = true;
		Refresh();
		PublishSelected();
	}

	partial void OnSelectedFormIndexChanged( int value )
	{
		if( value < 0 || value >= ThemePresets.Forms.Count )
			return;
		FormDescription = ThemePresets.Forms[ value ].Description;
		if( ThemePresets.Forms[ value ].Name == _theme.Form )
			return;
		_theme.SetForm( ThemePresets.Forms[ value ].Name );
		HasUnsavedChanges = true;
		Refresh();
	}

	partial void OnSelectedColorChanged( ThemeColorItem? value ) => PublishSelected();

	/// <summary>The color picker (or hex box) set a new value for the selected color.</summary>
	/// <param name="color">The new color; applied live but not saved.</param>
	/// <remarks>Ignored while colors can't be edited (<see cref="IsEditable"/> false), with no selection, or when the color is unchanged.</remarks>
	public void SetSelectedColor( ArgbColor color )
	{
		if( !IsEditable || SelectedColor is not { } item || item.Hex == color.ToString() )
			return;
		_theme.SetColor( item.Key, color );
		HasUnsavedChanges = true;
		Refresh( item );
		HexInput = item.Hex;
	}

	partial void OnHexInputChanged( string value )
	{
		if( IsEditable && SelectedColor is { } item && ArgbColor.TryParse( value, out var color ) && color.ToString() != item.Hex )
		{
			SetSelectedColor( color );
			SelectedColorValueChanged?.Invoke( color );
		}
	}

	/// <summary>Restores the selected color to the built-in value (not saved yet).</summary>
	[RelayCommand( CanExecute = nameof( CanResetSelected ) )]
	private void ResetSelected()
	{
		if( SelectedColor is not { } item )
			return;
		_theme.ResetColor( item.Key );
		HasUnsavedChanges = true;
		Refresh( item );
		PublishSelected();
	}

	private bool CanResetSelected() => IsEditable && SelectedColor is not null;

	/// <summary>Restores every color of the running theme to the built-in values (not saved yet).</summary>
	[RelayCommand( CanExecute = nameof( IsEditable ) )]
	private void ResetAll()
	{
		_theme.ResetAll();
		HasUnsavedChanges = true;
		Refresh();
		PublishSelected();
		StatusText = $"All {EditingThemeText.ToLowerInvariant()} colors restored to the defaults (not saved yet).";
	}

	/// <summary>Saves the theme to AppConfig.json; a failure is shown in the status text.</summary>
	[RelayCommand( CanExecute = nameof( HasUnsavedChanges ) )]
	private void Save()
	{
		try
		{
			_theme.Save();
			HasUnsavedChanges = false;
			StatusText = _theme.NeedsRestart || _theme.StyleNeedsRestart
				? $"Saved {DateTime.Now:t}. Restart HackPDM to finish switching to {ThemePresets.Palette( _theme.Palette ).Title} / {ThemePresets.Form( _theme.Form ).Title}."
				: $"Saved {DateTime.Now:t}. Loaded automatically at startup.";
		}
		catch( Exception e )
		{
			StatusText = $"Save failed: {e.Message}";
		}
	}

	/// <summary>Discards unsaved changes and re-applies the theme as last saved.</summary>
	[RelayCommand( CanExecute = nameof( HasUnsavedChanges ) )]
	private void Revert()
	{
		_theme.Revert();
		HasUnsavedChanges = false;
		SelectedPaletteIndex = PaletteIndex( _theme.Palette );
		SelectedFormIndex = FormIndex( _theme.Form );
		Refresh();
		PublishSelected();
		StatusText = "Unsaved changes discarded.";
	}

	private void Refresh( ThemeColorItem? only = null )
	{
		IEnumerable<ThemeColorItem> items = only is null ? Colors : [ only ];
		foreach( var item in items )
		{
			item.Hex = _theme.GetColor( item.Key )?.ToString() ?? "";
			item.IsOverridden = _theme.IsOverridden( item.Key );
		}
		EditingThemeText = ThemePresets.Palette( _theme.Palette ).Title;
		IsEditable = !_theme.NeedsRestart;
		List<string> notices = [];
		if( _theme.NeedsRestart )
			notices.Add( $"Previewing {EditingThemeText}. Save, then restart HackPDM to finish the switch and edit its colors (menus and dialogs also follow it after the restart)." );
		if( _theme.StyleNeedsRestart )
			notices.Add( $"Panels and the dependency graph already show the {ThemePresets.Form( _theme.Form ).Title} style; save and restart HackPDM for buttons, lists and other controls to take its corners and spacing." );
		RestartNotice = string.Join( " ", notices );
		ShowRestartNotice = notices.Count > 0;
	}

	private void PublishSelected()
	{
		ArgbColor? color = SelectedColor is { } item && ArgbColor.TryParse( item.Hex, out var c ) ? c : null;
		HexInput = color?.ToString() ?? "";
		SelectedColorValueChanged?.Invoke( color );
	}
}
