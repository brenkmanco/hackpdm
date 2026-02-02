using System.Collections;

using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;
// Resharper disable InconsistentNaming
namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_RECORD_STAGED_NAME, OdooDefaultsConstants.HP_RECORD_STAGED)]
public interface IHpRecordStagedModel : IHpOdooRecord
{
	[OdooProp(OdooFieldType.Char, "target_model")] public string? target_model { get;set;}
	[OdooProp(OdooFieldType.Many2One, "commit_id")] public IMany2One? commit_id { get; set; }
	[OdooProp(OdooFieldType.Integer, "target_id")] public int? target_id { get; set; }
	[OdooProp(OdooFieldType.Json, "payload")] public Hashtable? payload { get; set; }
}