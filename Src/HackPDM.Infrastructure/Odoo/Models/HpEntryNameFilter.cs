using System.Collections;
using HackPDM.Core.General;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

using OClient = HackPDM.Infrastructure.Odoo.OdooClient;

// Resharper disable InconsistentNaming

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_ENTRY_NAME_FILTER_NAME, OdooDefaultsConstants.HP_ENTRY_NAME_FILTER)]
public partial class HpEntryNameFilter : HpBaseModelTransport<HpEntryNameFilter>, IHpEntryNameFilterModel
{
	[OdooProp(OdooFieldType.Char, "name_proto")] public string? name_proto { get; set; }
	[OdooProp(OdooFieldType.Char, "name_regex")] public string? name_regex { get; set; }
	[OdooProp(OdooFieldType.Char, "description")] public string? description { get; set; }
}