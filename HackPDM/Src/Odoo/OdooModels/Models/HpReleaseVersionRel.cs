//using static System.Net.Mime.MediaTypeNames;

// ReSharper disable InconsistentNaming
namespace HackPDM.Odoo.OdooModels.Models;

[OdooModel(OdooDefaults.HP_RELEASE_VERSION_REL_NAME, OdooDefaults.HP_RELEASE_VERSION_REL)]
public partial class HpReleaseVersionRel : HpBaseModel<HpReleaseVersionRel>
{
    [OdooField(OdooFieldType.Many2one)] public int release_id;
    [OdooField(OdooFieldType.Many2one)] public int release_version;
	[OdooField(OdooFieldType.Many2one)] public int release_user;

    public HpReleaseVersionRel() { }
    public HpReleaseVersionRel(
        int releaseId = 0,
        int releaseVersion = 0,
        int releaseUser = 0)
    {
        this.release_id = releaseId;
        this.release_version = releaseVersion;
        this.release_user = releaseUser;
    }
}
public partial class HpReleaseVersionRel : HpBaseModel<HpReleaseVersionRel>
{
	
}
