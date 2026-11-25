using System.Text.Json.Serialization;

namespace WyrmpdmSync.Sync;

/// <summary>
/// A Penpot component variant (e.g. "Pane card" with Kind=Tinted) and the XAML style it stands for
/// (HackPaneTintedStyle in HackControls.xaml). Each synced setter keeps the value both sides had at the last sync.
/// </summary>
/// <remarks>
/// Components have a Light and a Dark variant (Theme property). The Light one is compared with the code; a push
/// writes both, each with its own preset's colors.
/// </remarks>
internal sealed class ComponentLink
{
	/// <summary>The Penpot component (variant set) name, e.g. "Pane card".</summary>
	public required string Component { get; set; }
	/// <summary>The variant's properties other than Theme, e.g. { "Kind": "Tinted" }; empty for single-variant components.</summary>
	public Dictionary<string, string> Variant { get; set; } = [];
	/// <summary>The XAML style key, e.g. "HackPaneTintedStyle".</summary>
	public required string Style { get; set; }
	/// <summary>
	/// The repository file defining the style, or null for a style that comes from Uno Material (FilledButtonStyle…):
	/// those links only record which code style a component stands for and are never synced.
	/// </summary>
	public string? File { get; set; }
	/// <summary>Synced setter → the value both sides had at the last sync (null when they never agreed).</summary>
	public Dictionary<string, string?> Properties { get; set; } = [];

	[JsonIgnore] public bool IsReference => File is null;
	[JsonIgnore] public string Label => Variant.Count == 0 ? Component : $"{Component} ({string.Join( ", ", Variant.Select( kv => $"{kv.Key}={kv.Value}" ) )})";
}

/// <summary>The component ↔ style pairs the design was built with; `init` adds any that are missing.</summary>
internal static class ComponentMap
{
	public static IReadOnlyList<ComponentLink> Defaults =>
	[
		Synced( "Pane card", "HackPaneTintedStyle", ( "Kind", "Tinted" ) ),
		Synced( "Pane card", "HackPaneOutlinedStyle", ( "Kind", "Outlined" ) ),
		Synced( "Toolbar", "HackToolbarStyle" ),
		Synced( "Banner", "HackBannerInfoStyle", ( "Kind", "Info" ) ),
		Synced( "Banner", "HackBannerWarningStyle", ( "Kind", "Warning" ) ),
		Synced( "Banner", "HackBannerErrorStyle", ( "Kind", "Error" ) ),
		Synced( "Icon button", "PaneIconButtonStyle", ( "State", "Standard" ) ),
		Synced( "Icon button", "PaneIconToggleButtonStyle", ( "State", "Selected" ) ),
		Synced( "Chip", "AssistChipButtonStyle", ( "Type", "Assist" ) ),
		Synced( "Text field", "HackOutlinedComboBoxStyle", ( "Kind", "Select" ) ),
		Reference( "Button", "FilledButtonStyle", ( "Type", "Filled" ) ),
		Reference( "Button", "FilledTonalButtonStyle", ( "Type", "Tonal" ) ),
		Reference( "Button", "OutlinedButtonStyle", ( "Type", "Outlined" ) ),
		Reference( "Button", "TextButtonStyle", ( "Type", "Text" ) ),
		Reference( "Chip", "FilterChipStyle", ( "Type", "Filter" ) ),
		Reference( "Text field", "OutlinedTextBoxStyle", ( "Kind", "Text" ) ),
	];

	private static ComponentLink Synced( string component, string style, params (string Key, string Value)[] variant )
		=> new() { Component = component, Style = style, File = XamlStyles.HackControlsPath, Variant = variant.ToDictionary( v => v.Key, v => v.Value ) };

	private static ComponentLink Reference( string component, string style, params (string Key, string Value)[] variant )
		=> new() { Component = component, Style = style, Variant = variant.ToDictionary( v => v.Key, v => v.Value ) };
}
