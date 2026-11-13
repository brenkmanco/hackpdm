using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using HackPDM.Abstractions;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>
/// The Odoo session: the connection settings (<see cref="Connection"/>), the logged-in user, and
/// the server's reference data (<see cref="Lookups"/>, loaded by <see cref="InitializeAsync"/>).
/// </summary>
public class OdooDefaults : IOdooDefaults
{
    /// <summary>The process-wide session used by <see cref="OdooRpc"/> and the models.</summary>
    /// <value>Starts as an instance with no settings provider; replace it or set its <see cref="Settings"/> before logging in.</value>
    public static IOdooDefaults Instance { get; set; } = new OdooDefaults();

    public OdooConnectionSettings Connection { get; private set; }
    /// <summary>The reference data; replaced as a whole by <see cref="InitializeAsync"/>.</summary>
    public OdooLookupCache Lookups { get; set; } = OdooLookupCache.Empty;

    private OdooDefaults() => Connection = new OdooConnectionSettings( null! );
    /// <summary>Creates a session over a settings provider.</summary>
    /// <param name="settingsProvider">The local settings the connection values are read from.</param>
    /// <remarks>
    /// Because <see cref="Instance"/> is never null, this constructor only hands <paramref name="settingsProvider"/> to the
    /// existing <see cref="Instance"/>; it does not make the new object the global instance.
    /// </remarks>
    public OdooDefaults(ISettingsProvider settingsProvider)
    {
        Connection = new OdooConnectionSettings( settingsProvider );
        if (Instance is not null)
        {
            Instance.Settings = settingsProvider;
            return;
        }
        Instance = this;
    }

    #region Connection
    public ISettingsProvider Settings { get => Connection.Settings; set => Connection.Settings = value; }
    public string? OdooUser { get => Connection.OdooUser; set => Connection.OdooUser = value; }
    public string? OdooPass { get => Connection.OdooPass; set => Connection.OdooPass = value; }
    public string? OdooAddress { get => Connection.OdooAddress; set => Connection.OdooAddress = value; }
    public string? OdooPort { get => Connection.OdooPort; set => Connection.OdooPort = value; }
    public string? OdooDb { get => Connection.OdooDb; set => Connection.OdooDb = value; }
    public string? OdooUrl { get => Connection.OdooUrl; set => Connection.OdooUrl = value; }
    public bool OdooUseHttps { get => Connection.OdooUseHttps; set => Connection.OdooUseHttps = value; }
    public string? OdooSwKey { get => Connection.OdooSwKey; set => Connection.OdooSwKey = value; }
    public decimal? OdooAreaFactor { get => Connection.OdooAreaFactor; set => Connection.OdooAreaFactor = value; }
    public string? OdooCredentialTarget { get => Connection.OdooCredentialTarget; set => Connection.OdooCredentialTarget = value; }
    /// <summary>Saves the current user name and password in the OS credential store; see <see cref="OdooConnectionSettings.SaveCredentials"/>.</summary>
    /// <returns>False when the user name or password is empty (nothing saved); otherwise true.</returns>
    public bool SaveCredentials() => Connection.SaveCredentials();
    /// <summary>Saves a login in the OS credential store; see <see cref="OdooConnectionSettings.TrySetWindowsOdooUserPass"/>.</summary>
    /// <param name="username">The Odoo user name.</param>
    /// <param name="password">The Odoo password.</param>
    /// <param name="credentialName">The credential name; the default target when null.</param>
    /// <returns>False when the user name or password is empty (nothing saved); otherwise true.</returns>
    public static bool TrySetWindowsOdooUserPass( string? username, string? password, string? credentialName = null )
        => OdooConnectionSettings.TrySetWindowsOdooUserPass( username, password, credentialName );
    #endregion

    #region Session
    public int OdooId { get; set; }
    /// <summary>Why the last login failed; null after a successful one.</summary>
    public string? LoginError { get; private set; }

    /// <summary>Logs in through <see cref="OdooRpc.LoginAsync"/> unless a user id is already set.</summary>
    /// <remarks>
    /// Uses a 7 second timeout. Sets <see cref="OdooId"/> (0 on failure) and <see cref="LoginError"/>; a failed login is
    /// not thrown. Does nothing when <see cref="OdooId"/> is already non-zero.
    /// </remarks>
    public async Task InitializeLoginAsync()
    {
        if( OdooId is not 0 )
            return;
        Result<int> login = await OdooRpc.LoginAsync( 7000 );
        LoginError = login.Error;
        OdooId = login.ValueOr( 0 );
    }

    /// <summary>Logs in when needed, then loads the server's reference data into <see cref="Lookups"/>; complete when this returns.</summary>
    /// <remarks>Individual lookups that fail are left empty by <see cref="OdooLookupCache.LoadAsync"/> rather than failing the whole call.</remarks>
    /// <exception cref="Exception">The login failed; the message includes <see cref="LoginError"/>.</exception>
    public async Task InitializeAsync()
    {
        await InitializeLoginAsync();
        if( OdooId is 0 )
            throw new Exception( $"login failed: {LoginError ?? "no user id"}" );
        Lookups = await OdooLookupCache.LoadAsync();
    }
    #endregion

    #region Lookups
    public IHpNodeModel? MyNode => Lookups.MyNode;
    public IHpDirectoryModel? HpDirectoryRoot => Lookups.HpDirectoryRoot;
    public string[] EntryFilterPatterns => Lookups.EntryFilterPatterns;
    public int DownloadBatchSize => Lookups.DownloadBatchSize;
    public int ConcurrencySize => Lookups.ConcurrencySize;
    public int? MaxConcurrency => Lookups.MaxConcurrency;
    public int? MaxBatchSize => Lookups.MaxBatchSize;
    public IHpSettingModel[]? HpSettings => Lookups.HpSettings;
    public string? SwApi => Lookups.SwApi;
    public bool? RestrictProperties => Lookups.RestrictProperties;
    public bool? RestrictTypes => Lookups.RestrictTypes;
    public IHpEntryNameFilterModel[]? HpEntryNameFilters => Lookups.HpEntryNameFilters;
    public IHpCategoryModel[]? HpCategories => Lookups.HpCategories;
    public IHpTypeModel[]? HpTypes => Lookups.HpTypes;
    public IHpPropertyModel[]? HpProperties => Lookups.HpProperties;
    public IHpNodeModel[]? HpNodes => Lookups.HpNodes;
    public IHpUserModel[]? HpUsers => Lookups.HpUsers;
    public Dictionary<string, IHpTypeModel> ExtToType => Lookups.ExtToType;
    public Dictionary<string, IHpCategoryModel> ExtToCat => Lookups.ExtToCat;
    public Dictionary<string, IHpPropertyModel> ExtToProp => Lookups.ExtToProp;
    public Dictionary<string, IHpEntryNameFilterModel> ExtToFilter => Lookups.ExtToFilter;
    public Dictionary<int, IHpPropertyModel> IdToProp => Lookups.IdToProp;
    public Dictionary<int, IHpUserModel> IdToUser => Lookups.IdToUser;
    #endregion
}
