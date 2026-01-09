using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_SETTINGS_NAME, OdooDefaultsConstants.HP_SETTINGS)]
public partial class HpSetting : HpBaseModelTransport<HpSetting>, IHpSettingModel
{
	[OdooProp(OdooFieldType.Char)] public string name { get; set; }
	[OdooProp(OdooFieldType.Char)] public string description { get; set; }
	[OdooProp(OdooFieldType.Char)] public string type { get; set; }
	[OdooProp(OdooFieldType.Boolean)] public bool bool_value { get; set; }
	[OdooProp(OdooFieldType.Integer)] public int int_value { get; set; }
	[OdooProp(OdooFieldType.Char)] public string char_value { get; set; }
	[OdooProp(OdooFieldType.Float)] public float float_value { get; set; }
	[OdooProp(OdooFieldType.DateTime)] public DateTime date_value { get; set; }
}
