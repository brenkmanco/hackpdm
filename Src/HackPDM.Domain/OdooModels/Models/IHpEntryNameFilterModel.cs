//using static System.Net.Mime.MediaTypeNames;

// Resharper disable InconsistentNaming

using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_ENTRY_NAME_FILTER_NAME, OdooDefaultsConstants.HP_ENTRY_NAME_FILTER)]
public interface IHpEntryNameFilterModel : IHpOdooRecord
{
	[OdooProp(OdooFieldType.Char, "name_proto")] public string? name_proto { get; set; }
	[OdooProp(OdooFieldType.Char, "name_regex")] public string? name_regex { get; set; }
	[OdooProp(OdooFieldType.Char, "description")] public string? description { get; set; }
}