using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Core.Pdm;
using HackPDM.Core.Services;
using HackPDM.Core.Theming;

using Microsoft.UI.Xaml.Data;

namespace HackPDM.Themes;

/// <summary>One editable theme color in the editor list.</summary>
[Bindable]
public sealed partial class ThemeColorItem( ThemeColorKey key, bool isGroupStart ) : ObservableObject
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
[Bindable]
public sealed partial class ThemeEditorViewModel : ObservableObject
{
	private static readonly string[] SharedPaletteTitles = [ .. ThemePresets.Palettes.Select( static p => p.Title ) ];
	private static readonly string[] SharedFormTitles = [ .. ThemePresets.Forms.Select( static f => f.Title ) ];

	private readonly ThemeService _theme;
	private readonly LoginSession? _session;

	public ThemeEditorViewModel( ThemeService theme, LoginSession? session = null )
	{
		_theme = theme;
		_session = session;
		string? previousGroup = null;
		foreach( var key in ThemeColorCatalog.All )
		{
			Colors.Add( new( key, key.Group != previousGroup ) );
			previousGroup = key.Group;
		}
		SelectedPaletteIndex = PaletteIndex( _theme.Palette );
		SelectedFormIndex = FormIndex( _theme.Form );
		ShowTreeBranchLines = _theme.ShowTreeBranchLines;
		Refresh();
		SelectedColor = Colors.Count > 0 ? Colors[ 0 ] : null;
	}

	/// <summary>The color presets, in menu order.</summary>
	public string[] PaletteTitles => SharedPaletteTitles;

	/// <summary>The shape presets, in menu order.</summary>
	public string[] FormTitles => SharedFormTitles;

	private static int PaletteIndex( PaletteName name )
	{
		var palettes = ThemePresets.Palettes;
		for( int i = 0; i < palettes.Count; i++ )
			if( palettes[ i ].Name == name )
				return i;
		return -1;
	}

	private static int FormIndex( FormName name )
	{
		var forms = ThemePresets.Forms;
		for( int i = 0; i < forms.Count; i++ )
			if( forms[ i ].Name == name )
				return i;
		return -1;
	}

	public ObservableCollection<ThemeColorItem> Colors { get; } = [];

	public string SavePath => _theme.SavePath;

	/// <summary>Raised when the selected color's value changes outside the color picker (selection, reset, mode switch).</summary>
	public event Action<ArgbColor?>? SelectedColorValueChanged;

	[ObservableProperty] public partial int SelectedPaletteIndex { get; set; } = -1;

	[ObservableProperty] public partial int SelectedFormIndex { get; set; } = -1;

	[ObservableProperty] public partial bool ShowTreeBranchLines { get; set; } = true;

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

	partial void OnShowTreeBranchLinesChanged( bool value )
	{
		if( value == _theme.ShowTreeBranchLines )
			return;
		_theme.SetShowTreeBranchLines( value );
		HasUnsavedChanges = true;
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
			string paletteTitle = ThemePresets.Palette( _theme.Palette ).Title;
			string formTitle = ThemePresets.Form( _theme.Form ).Title;
			StatusText = _theme.NeedsRestart || _theme.StyleNeedsRestart
				? $"Saved {DateTime.Now:t}. Restart HackPDM to finish switching to {paletteTitle} / {formTitle}."
				: $"Saved {DateTime.Now:t}. Loaded automatically at startup.";
			_session?.LogInfo( ProfileStatusCategory.Settings, $"Saved theme settings ({paletteTitle} / {formTitle}, branch lines: {(ShowTreeBranchLines ? "shown" : "hidden")})." );
			if( _theme.NeedsRestart || _theme.StyleNeedsRestart )
				_session?.LogWarning( ProfileStatusCategory.Settings, $"Restart HackPDM to finish applying {paletteTitle} / {formTitle} theme changes." );
		}
		catch( Exception e )
		{
			StatusText = $"Save failed: {e.Message}";
			_session?.LogError( ProfileStatusCategory.Settings, $"Failed to save theme settings to '{SavePath}': {e.Message}" );
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
		ShowTreeBranchLines = _theme.ShowTreeBranchLines;
		Refresh();
		PublishSelected();
		StatusText = "Unsaved changes discarded.";
		_session?.LogInfo( ProfileStatusCategory.Settings, "Reverted unsaved theme settings changes." );
	}

	private void Refresh( ThemeColorItem? only = null )
	{
		if( only is not null )
		{
			only.Hex = _theme.GetColor( only.Key )?.ToString() ?? "";
			only.IsOverridden = _theme.IsOverridden( only.Key );
		}
		else
		{
			for( int i = 0; i < Colors.Count; i++ )
			{
				ThemeColorItem item = Colors[ i ];
				item.Hex = _theme.GetColor( item.Key )?.ToString() ?? "";
				item.IsOverridden = _theme.IsOverridden( item.Key );
			}
		}
		EditingThemeText = ThemePresets.Palette( _theme.Palette ).Title;
		IsEditable = !_theme.NeedsRestart;
		bool needsMode = _theme.NeedsRestart;
		bool needsStyle = _theme.StyleNeedsRestart;
		string? modeNotice = needsMode
			? $"Previewing {EditingThemeText}. Save, then restart HackPDM to finish the switch and edit its colors (menus and dialogs also follow it after the restart)."
			: null;
		string? styleNotice = needsStyle
			? $"Panels and the dependency graph already show the {ThemePresets.Form( _theme.Form ).Title} style; save and restart HackPDM for buttons, lists and other controls to take its corners and spacing."
			: null;
		RestartNotice = ( modeNotice, styleNotice ) switch
		{
			( not null, not null ) => $"{modeNotice} {styleNotice}",
			( not null, null ) => modeNotice,
			( null, not null ) => styleNotice,
			_ => "",
		};
		ShowRestartNotice = needsMode || needsStyle;
	}

	private void PublishSelected()
	{
		ArgbColor? color = SelectedColor is { } item && ArgbColor.TryParse( item.Hex, out var c ) ? c : null;
		HexInput = color?.ToString() ?? "";
		SelectedColorValueChanged?.Invoke( color );
	}
}
