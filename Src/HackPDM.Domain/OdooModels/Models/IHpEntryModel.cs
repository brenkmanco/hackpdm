using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;
// Resharper disable InconsistentNaming
namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_ENTRY_NAME, OdooDefaultsConstants.HP_ENTRY)]
public interface IHpEntryModel : IHpOdooRecord
{
	[OdooProp(OdooFieldType.Char, "name")] public string? name {get;set;}
	[OdooProp(OdooFieldType.Char, "windows_complete_name")] public string? windows_complete_name { get; set; }
	[OdooProp(OdooFieldType.DateTime, "checkout_date")] public string? checkout_date {get;set;}
	[OdooProp(OdooFieldType.Boolean, "deleted")] public bool? deleted {get;set;}
	[OdooProp(OdooFieldType.Many2one, "latest_version_id")] public IMany2One? latest_version_id {get;set;}
	[OdooProp(OdooFieldType.Many2one, "dir_id")] public IMany2One? dir_id {get;set;}
	[OdooProp(OdooFieldType.Many2one, "type_id")] public IMany2One? type_id {get;set;}
	[OdooProp(OdooFieldType.Many2one, "cat_id")] public IMany2One? cat_id {get;set;}
	[OdooProp(OdooFieldType.Many2one, "checkout_user")] public IMany2One? checkout_user {get;set;}
	[OdooProp(OdooFieldType.Many2one, "checkout_node")] public IMany2One? checkout_node {get;set;}
    public bool IsLatest { get; }
}
public struct EntryLocalPath(string path, IHpEntryModel? entry, bool isBroken = false)
{
	public IHpEntryModel? Entry { get; set; } = entry;
	public string Path { get; set; } = path;
	public bool IsBroken { get; set; } = isBroken;
}