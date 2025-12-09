//using static System.Net.Mime.MediaTypeNames;

// ReSharper disable InconsistentNaming
namespace HackPDM.Odoo.OdooModels.Models;

public partial class HpReleaseVersionRel : HpBaseModel<HpReleaseVersionRel>
{
    public int release_id;
    public int release_version;
    public int release_user;

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
