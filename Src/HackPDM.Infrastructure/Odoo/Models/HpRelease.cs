using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_RELEASE_NAME, OdooDefaultsConstants.HP_RELEASE)]
public partial class HpRelease : HpBaseModelTransport<HpRelease>, IHpReleaseModel
{
	[OdooProp(OdooFieldType.Many2one)] public int version_id { get; set; }
	[OdooProp(OdooFieldType.Many2one)] public int release_user_id { get; set; }
	[OdooProp(OdooFieldType.DateTime)] public DateTime? release_stamp { get; set; }
	[OdooProp(OdooFieldType.Char)] public string release_note { get; set; }
}
