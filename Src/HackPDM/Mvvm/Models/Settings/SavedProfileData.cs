using System.Text.Json.Serialization;

namespace HackPDM.Mvvm.Models.Settings;

public class SavedProfileData
{
	[JsonPropertyName("OdooAddress")] public string? OdooAddress { get; set; }
	[JsonPropertyName("OdooPort")] public string? OdooPort { get; set; }
	/// <summary>Whether to connect with HTTPS; <see langword="null"/> (older profiles) leaves the current setting.</summary>
	[JsonPropertyName("OdooUseHttps")] public bool? OdooUseHttps { get; set; }
	[JsonPropertyName("OdooDb")] public string? OdooDb { get; set; }
	[JsonPropertyName("PWAPathAbsolute")] public string? PWAPathAbsolute { get; set; }
	[JsonPropertyName("OdooCredentialTarget")] public string? OdooCredentialTarget { get; set; }
	[JsonPropertyName("Username")] public string? Username { get; set; }
	[JsonPropertyName("Password")] public string? Password { get; set; }
}
