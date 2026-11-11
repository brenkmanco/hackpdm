using System.Collections.Generic;
using HackPDM.Abstractions;
using HackPDM.Domain.OdooModels.Models;

namespace HackPDM.Domain.OdooModels;

public interface IOdooDefaultBase
{
	ISettingsProvider Settings { get; set; }
	string? OdooUser { get; set; }
	string? OdooPass { get; set; }
	string? OdooAddress { get; set; }
	string? OdooPort { get; set; }
	string? OdooDb { get; set; }
	string? OdooUrl { get; set; }
	string? OdooSwKey { get; set; }
	decimal? OdooAreaFactor { get; set; }
	string? OdooCredentialTarget
	{
		// Settings.Get<string?>("OdooCredentialTarget", StorageBox.DEFAULT_ODOO_CREDENTIALS)
		get;
		set;
	}

	int OdooId { get; set; }
	/// <summary>Why the last login failed; null after a successful one.</summary>
	string? LoginError { get; }
	string[] EntryFilterPatterns { get; }
	IHpNodeModel? MyNode { get; }
	IHpDirectoryModel? HpDirectoryRoot { get; }
	int DownloadBatchSize { get; }
	int ConcurrencySize { get; }
	int? MaxConcurrency { get; }
	int? MaxBatchSize { get; }
	IHpSettingModel[]? HpSettings { get; }
	string? SwApi { get; }
	bool? RestrictProperties { get; }
	bool? RestrictTypes { get; }
	IHpEntryNameFilterModel[]? HpEntryNameFilters { get; }
	IHpCategoryModel[]? HpCategories { get; }
	IHpTypeModel[]? HpTypes { get; }
	IHpPropertyModel[]? HpProperties { get; }
	IHpNodeModel[]? HpNodes { get; }
	IHpUserModel[]? HpUsers { get; }
	Dictionary<string, IHpTypeModel> ExtToType { get; }
	Dictionary<string, IHpCategoryModel> ExtToCat { get; }
	Dictionary<string, IHpPropertyModel> ExtToProp { get; }
	Dictionary<string, IHpEntryNameFilterModel> ExtToFilter { get; }
	Dictionary<int, IHpPropertyModel> IdToProp { get; }
	Dictionary<int, IHpUserModel> IdToUser { get; }
	Task InitializeAsync();
}
public interface IOdooDefaults : IOdooDefaultBase
{
    public static virtual IOdooDefaults Instance { get; set; }
}