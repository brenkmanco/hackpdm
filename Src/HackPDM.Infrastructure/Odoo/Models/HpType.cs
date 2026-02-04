using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

namespace HackPDM.Infrastructure.Odoo.Models;


[OdooModel(OdooDefaultsConstants.HP_TYPE_NAME, OdooDefaultsConstants.HP_TYPE)]
public partial class HpType : HpBaseModelTransport<HpType>, IHpTypeModel
{
	[OdooProp(OdooFieldType.Char, "description")] public string? description { get; set; }
	[OdooProp(OdooFieldType.Char, "file_ext")] public string? file_ext { get; set; }
	[OdooProp(OdooFieldType.Char, "type_regex")] public string? type_regex { get; set; }

	[OdooProp(OdooFieldType.Binary, "icon")] public byte[]? icon { get; set; }

	[OdooProp(OdooFieldType.Many2One, "cat_id")] public Many2One? cat_id { get; set; }
	IMany2One? IHpTypeModel.cat_id { get =>(IMany2One?)cat_id; set => cat_id = (Many2One?)value; }
}
