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

	/// <summary>Captures the live Odoo and vault settings as a profile.</summary>
	/// <param name="includePassword">Whether the current password is copied; when <see langword="false"/> the profile's password is <see langword="null"/>.</param>
	/// <returns>A new profile; missing settings become empty strings.</returns>
	/// <remarks>Reads <see cref="HackPDM.Infrastructure.Odoo.OdooDefaults.Instance"/> and the vault path from <c>HackDefaults</c>.</remarks>
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
	/// <param name="profile">The profile to apply; empty or <see langword="null"/> fields leave the current setting unchanged.</param>
	/// <remarks>Changes <see cref="HackPDM.Infrastructure.Odoo.OdooDefaults.Instance"/> and the vault path in memory only; it does not log in or save anything.</remarks>
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

	/// <summary>Reads a profile from a JSON file.</summary>
	/// <param name="path">The profile file to read.</param>
	/// <returns>The profile in the file; <see langword="null"/> when the file holds the JSON literal <c>null</c>.</returns>
	/// <exception cref="System.IO.IOException">The file cannot be read (missing files raise <see cref="System.IO.FileNotFoundException"/>).</exception>
	/// <exception cref="System.Text.Json.JsonException">The file is not valid JSON for a profile.</exception>
	public static async Task<SavedProfileData?> ReadAsync( string path )
		=> JsonSerializer.Deserialize<SavedProfileData>( await File.ReadAllTextAsync( path, Encoding.UTF8 ) );

	/// <summary>Saves a profile to a JSON file, replacing the file if it exists.</summary>
	/// <param name="path">The file to write.</param>
	/// <param name="profile">The profile to save; a non-null password is written in plain text.</param>
	/// <exception cref="System.IO.IOException">The file cannot be written.</exception>
	/// <exception cref="System.UnauthorizedAccessException">The path is not writable by this user.</exception>
	public static Task WriteAsync( string path, SavedProfileData profile )
		=> File.WriteAllTextAsync( path, JsonSerializer.Serialize( profile ) );

	private static string? FirstNonEmpty( string? first, string? second )
		=> !string.IsNullOrEmpty( first ) ? first : !string.IsNullOrEmpty( second ) ? second : null;
}
