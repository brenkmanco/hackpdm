using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_RELEASE_VERSION_REL_NAME, OdooDefaultsConstants.HP_RELEASE_VERSION_REL)]
public partial class HpReleaseVersionRel : HpBaseModelTransport<HpReleaseVersionRel>, IHpReleaseVersionRelModel
{
	[OdooProp(OdooFieldType.Many2one, "release_id")] public Many2One? release_id { get; set; }
	IMany2One? IHpReleaseVersionRelModel.release_id { get =>(IMany2One?)release_id; set => release_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2one, "release_version")] public Many2One? release_version { get; set; }
	IMany2One? IHpReleaseVersionRelModel.release_version { get =>(IMany2One?)release_version; set => release_version = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2one, "release_user")] public Many2One? release_user { get; set; }
	IMany2One? IHpReleaseVersionRelModel.release_user { get =>(IMany2One?)release_user; set => release_user = (Many2One?)value; }
}
