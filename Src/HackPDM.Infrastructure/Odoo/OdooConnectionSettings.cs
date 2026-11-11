using HackPDM.Abstractions;
using HackPDM.Core;
using HackPDM.Shared.GlobalData;

using Meziantou.Framework.Win32;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>
/// Where the server is and who logs in: address, port and database from the local settings,
/// user and password from the OS credential store.
/// </summary>
public sealed class OdooConnectionSettings( ISettingsProvider settings )
{
    public ISettingsProvider Settings { get; set; } = settings;

	public string? OdooUser
    {
        get 
        {
			if (!string.IsNullOrEmpty(field)) return field;
			var cm = CrossPlatformCredentialManager.ReadCredential(OdooCredentialTarget ?? StorageBox.DEFAULT_ODOO_CREDENTIALS);
			field = cm?.UserName;
            return field;
        }
        set 
        {
            field = value;
        }
    }
    public string? OdooPass
    {
        get
        {
			if (field is not null) return field;
			// read from windows credential manager
			var cm = CrossPlatformCredentialManager.ReadCredential(OdooCredentialTarget ?? StorageBox.DEFAULT_ODOO_CREDENTIALS);
            field = cm?.Password;
            return field;
        }
        set
        {
            field = value;
        }
    }
    public bool SaveCredentials() => TrySetWindowsOdooUserPass( OdooUser, OdooPass, OdooCredentialTarget );
	public static bool TrySetWindowsOdooUserPass(string? username, string? password, string? credentialName = null)
    {
        if( string.IsNullOrEmpty( password ) || string.IsNullOrEmpty( username ))
            return false;

		CrossPlatformCredentialManager.WriteCredential( credentialName ?? StorageBox.DEFAULT_ODOO_CREDENTIALS, username, password );
        return true;
	}
    public string? OdooAddress
    {
        get => field ??= Settings.Get<string>("OdooAddress");
        set => Settings.Set("OdooAddress", field = value);
    }
    public string? OdooPort
    {
        get => field ??= Settings.Get<string>("OdooPort");
        set => Settings.Set("OdooPort", field = value);
        
    }
    public string? OdooDb 
    {
        get => field ??= Settings.Get<string>("OdooDb");
        set => Settings.Set("OdooDb", field = value);
    }
    public string? OdooUrl 
    {
        get 
        {
            if (field is not null) return field;
            string? port = Settings.Get<string>("OdooPort");
            port = port is null or "" ? "" : $":{port}";

            field = $"http://{OdooAddress}{port}";
            
            return field;
        }
        set
        {
            field = value;
        }
    }
    public string? OdooSwKey
    {
        get => field ??= Settings.Get<string>("SwLicenseKey");
        set => Settings.Set("SwLicenseKey", field = value);
    }
    public decimal? OdooAreaFactor
    {
        get => field ??= Settings.Get<decimal>("AreaFactor");
        set => Settings.Set("AreaFactor", field = value);
    }
    public string? OdooCredentialTarget 
    {
        // Settings.Get<string?>("OdooCredentialTarget", StorageBox.DEFAULT_ODOO_CREDENTIALS)
        get => field ??= Settings.Get( "OdooCredentialTarget", StorageBox.DEFAULT_ODOO_CREDENTIALS);
        set => Settings.Set("OdooCredentialTarget", field = value);
        
    }
}
