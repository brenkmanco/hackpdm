using System.Text.Json.Serialization;

using HackPDM.Mvvm.Models.Settings;

namespace HackPDM.Mvvm.Views.Common.Settings;

/// <summary>The app's saved UI settings (AppConfig.json). <see cref="AppConfigStore"/> reads and writes it.</summary>
public class GlobalSerializedData
{
	[JsonIgnore]
	public bool IsLoadedFromFile { get; set; }

	[JsonPropertyName( "WindowSettings" )]
	public WindowSettings Win { get; set; } = new();

	[JsonPropertyName( "OdooEntryColors" )]
	public ColorSettings EColors { get; set; } = new();

	[JsonPropertyName( "ColumnSettings" )]
	public OdooEntryColumns ColumnsSettings { get; set; } = new();

	[JsonPropertyName( "Theme" )]
	public ThemeSettings Theme { get; set; } = new();
}
