using HackPDM.Domain.OdooModels.Models;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;
// Resharper disable InconsistentNaming
namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_PDM_COMMIT_NAME, OdooDefaultsConstants.HP_PDM_COMMIT)]
public partial class HpPDMCommit : HpBaseModelTransport<HpPDMCommit>, IHpPDMCommitModel
{
	[OdooProp(OdooFieldType.Char, "name")] public string? name {get;set;}
	[OdooProp(OdooFieldType.Char, "job_uuid")] public string? job_uuid { get; set; }
	[OdooProp(OdooFieldType.DateTime, "committed_at")] public DateTime? committed_at { get;set;}
	[OdooProp(OdooFieldType.DateTime, "commit_finished_at")] public DateTime? commit_finished_at { get; set; }
	
	[OdooProp(OdooFieldType.Boolean, "committing")] public bool? committing { get;set;}
	[OdooProp(OdooFieldType.Boolean, "committed")] public bool? committed { get; set; }
	[OdooProp(OdooFieldType.Boolean, "errored")] public bool? errored { get; set; }

	[OdooProp(OdooFieldType.Text, "message_exception")] public string? message_exception { get; set; }
	[OdooProp(OdooFieldType.Text, "commit_summary")] public string? commit_summary { get; set; }

	[OdooProp(OdooFieldType.Integer, "progress_total")] public int? progress_total { get; set; }

	[OdooProp(OdooFieldType.Float, "duration_seconds")] public float? duration_seconds { get; set; }

	[OdooProp(OdooFieldType.Many2One, "node_by")] public Many2One? node_by { get; set; }
	
	[OdooProp(OdooFieldType.One2Many, "staged_ids")] public One2Many? staged_ids { get;set;}
	IMany2One? IHpPDMCommitModel.node_by { get => (IMany2One?)node_by; set => node_by = (Many2One?)value; }
	IOne2Many? IHpPDMCommitModel.staged_ids { get => (IOne2Many?)staged_ids; set => staged_ids = (One2Many?)value; }
}