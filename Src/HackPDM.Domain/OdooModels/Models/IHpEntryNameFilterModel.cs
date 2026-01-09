//using static System.Net.Mime.MediaTypeNames;

// Resharper disable InconsistentNaming

using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_ENTRY_NAME_FILTER_NAME, OdooDefaultsConstants.HP_ENTRY_NAME_FILTER)]
public interface IHpEntryNameFilterModel : IHpOdooRecord
{
	[OdooProp(OdooFieldType.Char)] public string name_proto { get; set; }
	[OdooProp(OdooFieldType.Char)] public string name_regex { get; set; }
	[OdooProp(OdooFieldType.Char)] public string description { get; set; }
}