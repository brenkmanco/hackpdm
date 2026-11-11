using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.ViewModels.Common;

namespace HackPDM.Mvvm.ViewModels.Settings;

/// <summary>One editable theme color in the editor list.</summary>
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

	public ThemeEditorViewModel( IThemeService theme )
	{
		_theme = theme;
		string? previousGroup = null;
		foreach( var key in ThemeColorCatalog.All )
		{
			Colors.Add( new( key, key.Group != previousGroup ) );
			previousGroup = key.Group;
		}
		SelectedModeIndex = Array.IndexOf( Modes, _theme.Mode );
		Refresh();
		SelectedColor = Colors.FirstOrDefault();
	}

	public AppThemeMode[] Modes { get; } = Enum.GetValues<AppThemeMode>();

	public ObservableCollection<ThemeColorItem> Colors { get; } = [];

	public string SavePath => _theme.SavePath;

	/// <summary>Raised when the selected color's value changes outside the color picker (selection, reset, mode switch).</summary>
	public event Action<ArgbColor?>? SelectedColorValueChanged;

	[ObservableProperty] public partial int SelectedModeIndex { get; set; }

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

	partial void OnSelectedModeIndexChanged( int value )
	{
		if( value < 0 || value >= Modes.Length || Modes[ value ] == _theme.Mode )
			return;
		_theme.SetMode( Modes[ value ] );
		HasUnsavedChanges = true;
		Refresh();
		PublishSelected();
	}

	partial void OnSelectedColorChanged( ThemeColorItem? value ) => PublishSelected();

	/// <summary>The color picker (or hex box) set a new value for the selected color.</summary>
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

	[RelayCommand( CanExecute = nameof( IsEditable ) )]
	private void ResetAll()
	{
		_theme.ResetAll();
		HasUnsavedChanges = true;
		Refresh();
		PublishSelected();
		StatusText = $"All {EditingThemeText.ToLowerInvariant()} colors restored to the defaults (not saved yet).";
	}

	[RelayCommand( CanExecute = nameof( HasUnsavedChanges ) )]
	private void Save()
	{
		try
		{
			_theme.Save();
			HasUnsavedChanges = false;
			StatusText = _theme.NeedsRestart
				? $"Saved {DateTime.Now:t}. Restart HackPDM to finish switching to {Modes[ SelectedModeIndex ]}."
				: $"Saved {DateTime.Now:t}. Loaded automatically at startup.";
		}
		catch( Exception e )
		{
			StatusText = $"Save failed: {e.Message}";
		}
	}

	[RelayCommand( CanExecute = nameof( HasUnsavedChanges ) )]
	private void Revert()
	{
		_theme.Revert();
		HasUnsavedChanges = false;
		SelectedModeIndex = Array.IndexOf( Modes, _theme.Mode );
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
		EditingThemeText = _theme.IsDarkActive ? "Dark" : "Light";
		IsEditable = !_theme.NeedsRestart;
		RestartNotice = IsEditable
			? ""
			: $"Previewing {_theme.Mode}. Save, then restart HackPDM to finish the switch and edit its colors (menus and dialogs also follow it after the restart).";
	}

	private void PublishSelected()
	{
		ArgbColor? color = SelectedColor is { } item && ArgbColor.TryParse( item.Hex, out var c ) ? c : null;
		HexInput = color?.ToString() ?? "";
		SelectedColorValueChanged?.Invoke( color );
	}
}
