//using static System.Net.Mime.MediaTypeNames;

// Resharper disable InconsistentNaming

using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_RELEASE_VERSION_REL_NAME, OdooDefaultsConstants.HP_RELEASE_VERSION_REL)]
public interface IHpReleaseVersionRelModel : IHpOdooRecord
{
    [OdooProp(OdooFieldType.Many2one, "release_id")] public IMany2One? release_id {get;set;}
    [OdooProp(OdooFieldType.Many2one, "release_version")] public IMany2One? release_version {get;set;}
	[OdooProp(OdooFieldType.Many2one, "release_user")] public IMany2One? release_user {get;set;}
}
