using HackPDM.Shared.GlobalData;

// Resharper disable InconsistentNaming


//using static System.Net.Mime.MediaTypeNames;



namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_VERSION_RELATIONSHIP_NAME, OdooDefaultsConstants.HP_VERSION_RELATIONSHIP)]
public interface IHpVersionRelationshipModel : IHpOdooRecord
{
    [OdooProp(OdooFieldType.Many2one)] public int parent_id {get;set;}
	[OdooProp(OdooFieldType.Many2one)] public int child_id {get;set;}
}