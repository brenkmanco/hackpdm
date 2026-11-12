using HackPDM.Abstractions;
using HackPDM.Core;
using HackPDM.Shared.GlobalData;

using Meziantou.Framework.Win32;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>
/// Where the server is and who logs in: address, port and database from the local settings,
/// user and password from the OS credential store.
/// </summary>
/// <param name="settings">The local settings store the address, port, database and other values are read from and written to.</param>
/// <remarks>
/// Every value is read once and cached; setting a property writes it through to <see cref="Settings"/>
/// (the user name and password are only cached until <see cref="SaveCredentials"/> stores them).
/// </remarks>
public sealed class OdooConnectionSettings( ISettingsProvider settings )
{
    public ISettingsProvider Settings { get; set; } = settings;

	/// <summary>The Odoo login name.</summary>
	/// <value>The assigned value, else the user name saved under <see cref="OdooCredentialTarget"/> via <see cref="CrossPlatformCredentialManager"/>; null when none is saved. Setting it does not save it.</value>
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
    /// <summary>The Odoo password.</summary>
    /// <value>The assigned value, else the password saved under <see cref="OdooCredentialTarget"/>; null when none is saved. Setting it does not save it.</value>
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
    /// <summary>Saves the current <see cref="OdooUser"/> and <see cref="OdooPass"/> under <see cref="OdooCredentialTarget"/> in the OS credential store.</summary>
    /// <returns>False when the user name or password is empty (nothing is saved); otherwise true.</returns>
    public bool SaveCredentials() => TrySetWindowsOdooUserPass( OdooUser, OdooPass, OdooCredentialTarget );
	/// <summary>Saves a login in the OS credential store (despite the name, on every platform, through <see cref="CrossPlatformCredentialManager.WriteCredential(string, string, string)"/>).</summary>
	/// <param name="username">The Odoo user name.</param>
	/// <param name="password">The Odoo password.</param>
	/// <param name="credentialName">The credential name; <c>StorageBox.DEFAULT_ODOO_CREDENTIALS</c> when null.</param>
	/// <returns>False when <paramref name="username"/> or <paramref name="password"/> is empty (nothing is saved); otherwise true, even when the login fell back to the credentials file.</returns>
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
    /// <summary>The server's base URL, used by <see cref="OdooRpc"/> for every call.</summary>
    /// <value>Built once as <c>http://{OdooAddress}:{port}</c> (no port part when the OdooPort setting is empty) unless set explicitly; later address or port changes don't update a URL already built.</value>
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
