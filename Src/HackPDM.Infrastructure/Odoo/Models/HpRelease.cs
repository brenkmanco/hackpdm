using System;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_RELEASE_NAME, OdooDefaultsConstants.HP_RELEASE)]
public partial class HpRelease : HpBaseModelTransport<HpRelease>, IHpReleaseModel
{
	[OdooProp(OdooFieldType.Many2one, "version_id")] public Many2One? version_id { get; set; }
	IMany2One? IHpReleaseModel.version_id { get =>(IMany2One?)version_id; set => version_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2one, "release_user_id")] public Many2One? release_user_id { get; set; }
	IMany2One? IHpReleaseModel.release_user_id { get =>(IMany2One?)release_user_id; set => release_user_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.DateTime, "release_stamp")] public DateTime? release_stamp { get; set; }
	[OdooProp(OdooFieldType.Char, "release_note")] public string? release_note { get; set; }
}
