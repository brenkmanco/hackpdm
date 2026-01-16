using System;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_SETTINGS_NAME, OdooDefaultsConstants.HP_SETTINGS)]
public partial class HpSetting : HpBaseModelTransport<HpSetting>, IHpSettingModel
{
	[OdooProp(OdooFieldType.Char, "name")] public string? name { get; set; }
	[OdooProp(OdooFieldType.Char, "description")] public string? description { get; set; }
	[OdooProp(OdooFieldType.Char, "type")] public string? type { get; set; }
	[OdooProp(OdooFieldType.Boolean, "bool_value")] public bool? bool_value { get; set; }
	[OdooProp(OdooFieldType.Integer, "int_value")] public int? int_value { get; set; }
	[OdooProp(OdooFieldType.Char, "char_value")] public string? char_value { get; set; }
	[OdooProp(OdooFieldType.Float, "float_value")] public float? float_value { get; set; }
	[OdooProp(OdooFieldType.DateTime, "date_value")] public DateTime? date_value { get; set; }
}
