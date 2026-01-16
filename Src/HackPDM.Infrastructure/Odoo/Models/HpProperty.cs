using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_PROPERTY_NAME, OdooDefaultsConstants.HP_PROPERTY)]
public partial class HpProperty : HpBaseModelTransport<HpProperty>, IHpPropertyModel
{
	[OdooProp(OdooFieldType.Char, "name")] public string? name { get; set; }
	[OdooProp(OdooFieldType.Char, "prop_type")] public string? prop_type { get; set; }
	[OdooProp(OdooFieldType.Boolean, "active")] public bool? active { get; set; }
}
