using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Pdm;

namespace HackPDM.Core.Services;

public sealed class SavedProfileData
{
	[JsonPropertyName( "OdooAddress" )] public string? OdooAddress { get; set; }
	[JsonPropertyName( "OdooPort" )] public string? OdooPort { get; set; }
	[JsonPropertyName( "OdooUseHttps" )] public bool? OdooUseHttps { get; set; }
	[JsonPropertyName( "OdooDb" )] public string? OdooDb { get; set; }
	[JsonPropertyName( "PWAPathAbsolute" )] public string? PWAPathAbsolute { get; set; }
	[JsonPropertyName( "OdooCredentialTarget" )] public string? OdooCredentialTarget { get; set; }
	[JsonPropertyName( "Username" )] public string? Username { get; set; }
	[JsonPropertyName( "Password" )] public string? Password { get; set; }
}

/// <summary>Loads, saves, and applies JSON connection profiles.</summary>
public static class ProfileStore
{
	private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

	public static SavedProfileData Template => new()
	{
		OdooAddress = "10.0.0.68",
		OdooCredentialTarget = PdmConstants.DefaultOdooCredentials,
		OdooDb = "odoopdm",
		OdooPort = "443",
		OdooUseHttps = true,
		PWAPathAbsolute = @"C:\path\to\pwa",
		Username = "{username}",
		Password = "{password} - leave blank to keep the password out of this file. "
			+ "After a login, the password is saved in the system keychain under OdooCredentialTarget.",
	};

	public static SavedProfileData Current( bool includePassword )
	{
		OdooDefaults odoo = OdooDefaults.Instance;
		return new()
		{
			OdooAddress = odoo.OdooAddress ?? "",
			OdooPort = odoo.OdooPort ?? "",
			OdooUseHttps = odoo.OdooUseHttps,
			OdooDb = odoo.OdooDb ?? "",
			OdooCredentialTarget = odoo.OdooCredentialTarget ?? "",
			PWAPathAbsolute = HackDefaults.Instance.PwaPathAbsolute,
			Username = odoo.OdooUser ?? "",
			Password = includePassword ? odoo.OdooPass ?? "" : null,
		};
	}

	public static void Apply( SavedProfileData profile )
	{
		OdooDefaults odoo = OdooDefaults.Instance;
		if( !string.IsNullOrEmpty( profile.OdooAddress ) )
			odoo.OdooAddress = profile.OdooAddress;
		if( !string.IsNullOrEmpty( profile.OdooPort ) )
			odoo.OdooPort = profile.OdooPort;
		if( !string.IsNullOrEmpty( profile.OdooDb ) )
			odoo.OdooDb = profile.OdooDb;
		if( profile.OdooUseHttps is bool useHttps )
			odoo.OdooUseHttps = useHttps;
		if( !string.IsNullOrEmpty( profile.OdooCredentialTarget ) )
			odoo.OdooCredentialTarget = profile.OdooCredentialTarget;
		if( !string.IsNullOrEmpty( profile.PWAPathAbsolute ) )
			HackDefaults.Instance.PwaPathAbsolute = profile.PWAPathAbsolute;

		string? username = FirstNonEmpty( profile.Username, odoo.OdooUser );
		string? password = FirstNonEmpty( profile.Password, odoo.OdooPass );
		if( username is null || password is null )
			return;
		odoo.OdooUser = username;
		odoo.OdooPass = password;
	}

	public static async Task<SavedProfileData?> ReadAsync( string path )
		=> JsonSerializer.Deserialize<SavedProfileData>( await File.ReadAllTextAsync( path, Encoding.UTF8 ) );

	public static Task WriteAsync( string path, SavedProfileData profile )
		=> File.WriteAllTextAsync( path, JsonSerializer.Serialize( profile, WriteOptions ) );

	private static string? FirstNonEmpty( string? first, string? second )
		=> !string.IsNullOrEmpty( first ) ? first : !string.IsNullOrEmpty( second ) ? second : null;
}
