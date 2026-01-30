using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

// Resharper disable InconsistentNaming


//using static System.Net.Mime.MediaTypeNames;



namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_VERSION_RELATIONSHIP_NAME, OdooDefaultsConstants.HP_VERSION_RELATIONSHIP)]
public interface IHpVersionRelationshipModel : IHpOdooRecord
{
    [OdooProp(OdooFieldType.Many2One, "parent_id")] public IMany2One? parent_id {get;set;}
	[OdooProp(OdooFieldType.Many2One, "child_id")] public IMany2One? child_id {get;set;}
}