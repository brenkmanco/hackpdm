using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Mvvm.Models.Settings;

/// <summary>A login profile (server, database, vault folder and user) as a JSON file, and moving it in and out of the live settings.</summary>
public static class ProfileStore
{
	/// <summary>An example profile with placeholders, for a new user to fill in.</summary>
	public static SavedProfileData Template => new()
	{
		OdooAddress = "10.0.0.68",
		OdooCredentialTarget = StorageBox.DEFAULT_ODOO_CREDENTIALS,
		OdooDb = "odoopdm",
		OdooPort = "8069",
		PWAPathAbsolute = @"C:\path\to\pwa",
		Username = "{username}",
		Password = "{password} - leave blank to keep the password out of this file. " +
			"After a login, the password is saved in the system keychain (Windows Credential Manager, " +
			"the Secret Service on Linux, the macOS Keychain) under OdooCredentialTarget.",
	};

	/// <summary>The live settings as a profile; the password only when <paramref name="includePassword"/>.</summary>
	public static SavedProfileData Current( bool includePassword )
	{
		var odoo = OdooDefaults.Instance;
		return new()
		{
			OdooAddress = odoo.OdooAddress ?? "",
			OdooPort = odoo.OdooPort ?? "",
			OdooDb = odoo.OdooDb ?? "",
			OdooCredentialTarget = odoo.OdooCredentialTarget ?? "",
			PWAPathAbsolute = HackDefaults.Instance?.PwaPathAbsolute ?? "",
			Username = odoo.OdooUser ?? "",
			Password = includePassword ? odoo.OdooPass ?? "" : null,
		};
	}

	/// <summary>
	/// Copies the profile's non-empty values into the live settings. The user and password are set
	/// together, each falling back to the current one, and only when both are known.
	/// </summary>
	public static void Apply( SavedProfileData profile )
	{
		var odoo = OdooDefaults.Instance;
		if( !string.IsNullOrEmpty( profile.OdooAddress ) )
			odoo.OdooAddress = profile.OdooAddress;
		if( !string.IsNullOrEmpty( profile.OdooPort ) )
			odoo.OdooPort = profile.OdooPort;
		if( !string.IsNullOrEmpty( profile.OdooDb ) )
			odoo.OdooDb = profile.OdooDb;
		if( !string.IsNullOrEmpty( profile.OdooCredentialTarget ) )
			odoo.OdooCredentialTarget = profile.OdooCredentialTarget;
		if( !string.IsNullOrEmpty( profile.PWAPathAbsolute ) )
			HackDefaults.Instance?.PwaPathAbsolute = profile.PWAPathAbsolute;

		string? username = FirstNonEmpty( profile.Username, odoo.OdooUser );
		string? password = FirstNonEmpty( profile.Password, odoo.OdooPass );
		if( username is null || password is null )
			return;
		odoo.OdooUser = username;
		odoo.OdooPass = password;
	}

	/// <summary>The profile in the file; null when it holds none.</summary>
	public static async Task<SavedProfileData?> ReadAsync( string path )
		=> JsonSerializer.Deserialize<SavedProfileData>( await File.ReadAllTextAsync( path, Encoding.UTF8 ) );

	public static Task WriteAsync( string path, SavedProfileData profile )
		=> File.WriteAllTextAsync( path, JsonSerializer.Serialize( profile ) );

	private static string? FirstNonEmpty( string? first, string? second )
		=> !string.IsNullOrEmpty( first ) ? first : !string.IsNullOrEmpty( second ) ? second : null;
}
