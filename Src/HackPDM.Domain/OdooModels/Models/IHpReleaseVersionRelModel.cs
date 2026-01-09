//using static System.Net.Mime.MediaTypeNames;

// Resharper disable InconsistentNaming

using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_RELEASE_VERSION_REL_NAME, OdooDefaultsConstants.HP_RELEASE_VERSION_REL)]
public interface IHpReleaseVersionRelModel : IHpOdooRecord
{
    [OdooProp(OdooFieldType.Many2one)] public int release_id {get;set;}
    [OdooProp(OdooFieldType.Many2one)] public int release_version {get;set;}
	[OdooProp(OdooFieldType.Many2one)] public int release_user {get;set;}
}
