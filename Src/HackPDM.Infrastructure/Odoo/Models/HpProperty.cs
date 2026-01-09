using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_PROPERTY_NAME, OdooDefaultsConstants.HP_PROPERTY)]
public partial class HpProperty : HpBaseModelTransport<HpProperty>, IHpPropertyModel
{
	[OdooProp(OdooFieldType.Char)] public string name { get; set; }
	[OdooProp(OdooFieldType.Char)] public string prop_type { get; set; }
	[OdooProp(OdooFieldType.Boolean)] public bool active { get; set; }
}
