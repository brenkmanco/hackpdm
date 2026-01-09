using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_RELEASE_VERSION_REL_NAME, OdooDefaultsConstants.HP_RELEASE_VERSION_REL)]
public partial class HpReleaseVersionRel : HpBaseModelTransport<HpReleaseVersionRel>, IHpReleaseVersionRelModel
{
	[OdooProp(OdooFieldType.Many2one)] public int release_id { get; set; }
	[OdooProp(OdooFieldType.Many2one)] public int release_version { get; set; }
	[OdooProp(OdooFieldType.Many2one)] public int release_user { get; set; }
}
