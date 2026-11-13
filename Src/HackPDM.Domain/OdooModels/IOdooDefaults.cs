using System.Collections.Generic;
using HackPDM.Abstractions;
using HackPDM.Domain.OdooModels.Models;

namespace HackPDM.Domain.OdooModels;

/// <summary>
/// The Odoo session: connection settings, the logged-in user, and the server's reference data (types, categories,
/// properties, users, settings) loaded once by <see cref="InitializeAsync"/>.
/// </summary>
public interface IOdooDefaultBase
{
	/// <summary>The local settings store the connection values are read from and saved to.</summary>
	ISettingsProvider Settings { get; set; }
	string? OdooUser { get; set; }
	string? OdooPass { get; set; }
	string? OdooAddress { get; set; }
	string? OdooPort { get; set; }
	string? OdooDb { get; set; }
	/// <summary>The server's base URL, which the JSON-RPC endpoint path is appended to.</summary>
	string? OdooUrl { get; set; }
	/// <summary>Whether to connect with HTTPS; when <see langword="false"/> the user accepts sending the password unencrypted.</summary>
	bool OdooUseHttps { get; set; }
	/// <summary>The SolidWorks Document Manager license key used to read SolidWorks files.</summary>
	string? OdooSwKey { get; set; }
	decimal? OdooAreaFactor { get; set; }
	string? OdooCredentialTarget
	{
		// Settings.Get<string?>("OdooCredentialTarget", StorageBox.DEFAULT_ODOO_CREDENTIALS)
		get;
		set;
	}

	/// <summary>The logged-in user's res.users id; 0 when not logged in.</summary>
	int OdooId { get; set; }
	/// <summary>Why the last login failed; null after a successful one.</summary>
	string? LoginError { get; }
	/// <summary>The name patterns (regular expressions) of hp.entry.name.filter records; matching files are ignored.</summary>
	string[] EntryFilterPatterns { get; }
	/// <summary>The hp.node record of this machine, used as the node of commits and checkouts.</summary>
	IHpNodeModel? MyNode { get; }
	/// <summary>The root hp.directory record.</summary>
	IHpDirectoryModel? HpDirectoryRoot { get; }
	/// <summary>How many files to download per batch: the server's <c>max_batch_size</c> setting when positive, otherwise a built-in default.</summary>
	int DownloadBatchSize { get; }
	/// <summary>How many requests to run in parallel: the server's <c>max_concurrency</c> setting when positive, otherwise a built-in default.</summary>
	int ConcurrencySize { get; }
	int? MaxConcurrency { get; }
	int? MaxBatchSize { get; }
	IHpSettingModel[]? HpSettings { get; }
	string? SwApi { get; }
	/// <summary>Server setting: only properties known to the server are stored; <see langword="null"/> when the settings could not be read.</summary>
	bool? RestrictProperties { get; }
	/// <summary>Server setting: only files of registered types may be committed; <see langword="null"/> when the settings could not be read.</summary>
	bool? RestrictTypes { get; }
	IHpEntryNameFilterModel[]? HpEntryNameFilters { get; }
	IHpCategoryModel[]? HpCategories { get; }
	IHpTypeModel[]? HpTypes { get; }
	IHpPropertyModel[]? HpProperties { get; }
	IHpNodeModel[]? HpNodes { get; }
	IHpUserModel[]? HpUsers { get; }
	/// <summary>The registered file types keyed by extension.</summary>
	Dictionary<string, IHpTypeModel> ExtToType { get; }
	/// <summary>The categories of the registered file types, keyed by extension.</summary>
	Dictionary<string, IHpCategoryModel> ExtToCat { get; }
	/// <summary>The known hp.property records keyed by property name (despite the "Ext" prefix).</summary>
	Dictionary<string, IHpPropertyModel> ExtToProp { get; }
	/// <summary>The entry name filters keyed by their <c>name_proto</c>.</summary>
	Dictionary<string, IHpEntryNameFilterModel> ExtToFilter { get; }
	/// <summary>The known hp.property records keyed by id.</summary>
	Dictionary<int, IHpPropertyModel> IdToProp { get; }
	/// <summary>The users keyed by id.</summary>
	Dictionary<int, IHpUserModel> IdToUser { get; }
	/// <summary>Logs in when needed and then loads the server's reference data, replacing what was loaded before.</summary>
	/// <remarks>The lookup properties of this interface are empty or <see langword="null"/> until this has completed.</remarks>
	Task InitializeAsync();
}
/// <summary>An <see cref="IOdooDefaultBase"/> with a process-wide instance.</summary>
public interface IOdooDefaults : IOdooDefaultBase
{
    /// <summary>The process-wide session; every implementation keeps its own.</summary>
    public static abstract IOdooDefaults Instance { get; set; }
}