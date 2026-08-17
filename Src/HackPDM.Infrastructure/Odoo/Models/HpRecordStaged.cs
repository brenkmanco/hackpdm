using System.Collections;

using HackPDM.Domain.OdooModels.Models;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;
// Resharper disable InconsistentNaming
namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_RECORD_STAGED_NAME, OdooDefaultsConstants.HP_RECORD_STAGED)]
public partial class HpRecordStaged : HpBaseModelTransport<HpRecordStaged>, IHpRecordStagedModel
{
	[OdooProp(OdooFieldType.Char, "target_model")] public string? target_model { get;set;}
	[OdooProp(OdooFieldType.Integer, "target_id")] public int? target_id { get; set; }
	[OdooProp(OdooFieldType.Json, "payload")] public Hashtable? payload { get; set; }

	[OdooProp(OdooFieldType.Many2One, "committing_id")] public Many2One? committing_id { get; set; }
	[OdooProp(OdooFieldType.Many2Many, "dependency_tree_ids")] public Many2Many? dependency_tree_ids { get; set; }

	IMany2One? IHpRecordStagedModel.committing_id { get => (IMany2One?)committing_id; set => committing_id = (Many2One?)value; }
	IMany2Many? IHpRecordStagedModel.dependency_tree_ids { get => (IMany2Many?)dependency_tree_ids; set => dependency_tree_ids = (Many2Many?)value; }
}