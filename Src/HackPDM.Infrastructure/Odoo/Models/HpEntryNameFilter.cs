using System.Collections;
using HackPDM.Core.General;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Shared.GlobalData;
using OClient = HackPDM.Infrastructure.Odoo.OdooClient;

// Resharper disable InconsistentNaming

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_ENTRY_NAME_FILTER_NAME, OdooDefaultsConstants.HP_ENTRY_NAME_FILTER)]
public partial class HpEntryNameFilter : HpBaseModelTransport<HpEntryNameFilter>, IHpEntryNameFilterModel
{
	[OdooProp(OdooFieldType.Char)] public string name_proto { get; set; }
	[OdooProp(OdooFieldType.Char)] public string name_regex { get; set; }
	[OdooProp(OdooFieldType.Char)] public string description { get; set; }
}