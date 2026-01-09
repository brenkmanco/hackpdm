using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Infrastructure.Odoo.Models;


[OdooModel(OdooDefaultsConstants.HP_TYPE_NAME, OdooDefaultsConstants.HP_TYPE)]
public partial class HpType : HpBaseModelTransport<HpType>, IHpTypeModel
{
	[OdooProp(OdooFieldType.Char)] public string? description { get; set; }
	[OdooProp(OdooFieldType.Char)] public string? file_ext { get; set; }
	[OdooProp(OdooFieldType.Char)] public string? type_regex { get; set; }

	[OdooProp(OdooFieldType.Binary)] public string? icon { get; set; }

	[OdooProp(OdooFieldType.Many2one)] public int? cat_id { get; set; }
}
