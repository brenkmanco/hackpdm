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
	string[] EntryFilterPatterns { get; }
	IHpNodeModel? MyNode { get; }
	IHpDirectoryModel? HpDirectoryRoot { get; set; }
	int DownloadBatchSize { get; set; }
	int ConcurrencySize { get; }
	int? MaxConcurrency { get; }
	int? MaxBatchSize { get; }
	IHpSettingModel[]? HpSettings { get; set; }
	string? SwApi { get; set; }
	bool? RestrictProperties { get; set; }
	bool? RestrictTypes { get; set; }
	IHpEntryNameFilterModel[]? HpEntryNameFilters { get; set; }
	IHpCategoryModel[]? HpCategories { get; set; }
	IHpTypeModel[]? HpTypes { get; set; }
	IHpPropertyModel[]? HpProperties { get; set; }
	IHpNodeModel[]? HpNodes { get; set; }
	IHpUserModel[]? HpUsers { get; set; }
	Dictionary<string, IHpTypeModel> ExtToType { get; set; }
	Dictionary<string, IHpCategoryModel> ExtToCat { get; set; }
	Dictionary<string, IHpPropertyModel>? ExtToProp { get; set; }
	Dictionary<string, IHpEntryNameFilterModel> ExtToFilter { get; set; }
	Dictionary<int, IHpPropertyModel> IdToProp { get; set; }
	Dictionary<int, IHpUserModel> IdToUser { get; set; }
}
public interface IOdooDefaults : IOdooDefaultBase
{
    public static virtual IOdooDefaults? Instance { get; set; }
}