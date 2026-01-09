using HackPDM.Shared.GlobalData;
// Resharper disable InconsistentNaming
namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_ENTRY_NAME, OdooDefaultsConstants.HP_ENTRY)]
public interface IHpEntryModel : IHpOdooRecord
{
	[OdooProp(OdooFieldType.Char)] public string name {get;set;}
	[OdooProp(OdooFieldType.Char)] public string? windows_complete_name { get; set; }
	[OdooProp(OdooFieldType.DateTime)] public string checkout_date {get;set;}
	[OdooProp(OdooFieldType.Boolean)] public bool deleted {get;set;}
	[OdooProp(OdooFieldType.Many2one)] public int latest_version_id {get;set;}
	[OdooProp(OdooFieldType.Many2one)] public int dir_id {get;set;}
	[OdooProp(OdooFieldType.Many2one)] public int type_id {get;set;}
	[OdooProp(OdooFieldType.Many2one)] public int cat_id {get;set;}
	[OdooProp(OdooFieldType.Many2one)] public int? checkout_user {get;set;}
	[OdooProp(OdooFieldType.Many2one)] public int? checkout_node {get;set;}
    public bool IsLatest { get; }
}
public struct EntryLocalPath(string path, IHpEntryModel? entry, bool isBroken = false)
{
	public IHpEntryModel? Entry { get; set; } = entry;
	public string Path { get; set; } = path;
	public bool IsBroken { get; set; } = isBroken;
}