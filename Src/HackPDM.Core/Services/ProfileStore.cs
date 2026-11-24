using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Pdm;

namespace HackPDM.Core.Services;

public sealed class SavedProfileData
{
	[JsonPropertyName( nameof( OdooAddress ) )] public string? OdooAddress { get; set; }
	[JsonPropertyName( nameof( OdooPort ) )] public string? OdooPort { get; set; }
	[JsonPropertyName( nameof( OdooUseHttps ) )] public bool? OdooUseHttps { get; set; }
	[JsonPropertyName( nameof( OdooDb ) )] public string? OdooDb { get; set; }
	[JsonPropertyName( nameof( PWAPathAbsolute ) )] public string? PWAPathAbsolute { get; set; }
	[JsonPropertyName( nameof( OdooCredentialTarget ) )] public string? OdooCredentialTarget { get; set; }
	[JsonPropertyName( nameof( Username ) )] public string? Username { get; set; }
	[JsonPropertyName( nameof( Password ) )] public string? Password { get; set; }
}

[JsonSourceGenerationOptions( WriteIndented = true )]
[JsonSerializable( typeof( SavedProfileData ) )]
internal partial class ProfileJsonContext : JsonSerializerContext
{
}

/// <summary>Loads, saves, and applies JSON connection profiles.</summary>
public static class ProfileStore
{
	private static readonly Lock ApplyGate = new();

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
		ArgumentNullException.ThrowIfNull( profile );
		lock( ApplyGate )
		{
			OdooDefaults odoo = OdooDefaults.Instance;
			if( profile.OdooAddress is not null )
				odoo.OdooAddress = profile.OdooAddress;
			if( profile.OdooPort is not null )
				odoo.OdooPort = profile.OdooPort;
			if( profile.OdooDb is not null )
				odoo.OdooDb = profile.OdooDb;
			if( profile.OdooUseHttps is bool useHttps )
				odoo.OdooUseHttps = useHttps;
			if( profile.OdooCredentialTarget is not null )
			{
				odoo.OdooCredentialTarget = string.IsNullOrEmpty( profile.OdooCredentialTarget )
					? PdmConstants.DefaultOdooCredentials
					: profile.OdooCredentialTarget;
			}
			if( profile.PWAPathAbsolute is not null )
				HackDefaults.Instance.PwaPathAbsolute = profile.PWAPathAbsolute;

			odoo.OdooUrl = null;
			odoo.OdooId = 0;
			odoo.Lookups = OdooLookupCache.Empty;

			if( FirstNonEmpty( profile.Username, odoo.OdooUser ) is { } username )
				odoo.OdooUser = username;
			if( FirstNonEmpty( profile.Password, odoo.OdooPass ) is { } password )
				odoo.OdooPass = password;
		}
	}

	public static async Task<SavedProfileData?> ReadAsync( string path )
		=> JsonSerializer.Deserialize( await File.ReadAllTextAsync( path, Encoding.UTF8 ), ProfileJsonContext.Default.SavedProfileData );

	public static async Task WriteAsync( string path, SavedProfileData profile )
	{
		ArgumentNullException.ThrowIfNull( profile );
		string fullPath = Path.GetFullPath( path );
		if( Path.GetDirectoryName( fullPath ) is { Length: > 0 } dir )
			Directory.CreateDirectory( dir );

		string tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
		try
		{
			await using( FileStream stream = new( tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true ) )
			{
				await JsonSerializer.SerializeAsync( stream, profile, ProfileJsonContext.Default.SavedProfileData );
				await stream.FlushAsync();
			}
			if( !OperatingSystem.IsWindows() && !string.IsNullOrEmpty( profile.Password ) )
				File.SetUnixFileMode( tempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite );
			File.Move( tempPath, fullPath, overwrite: true );
		}
		catch
		{
			FileOps.TryDelete( tempPath );
			throw;
		}
	}

	private static string? FirstNonEmpty( string? first, string? second )
		=> !string.IsNullOrEmpty( first ) ? first : !string.IsNullOrEmpty( second ) ? second : null;
}
