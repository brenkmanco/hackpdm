using System.Collections;

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
public partial class HpPDMCommit
{ 
	/// <summary>
	/// Asks the server to apply this commit: the hp.record.staged rows staged in it become real records.
	/// </summary>
	/// <returns>
	/// <c>Committed</c> is <see langword="true"/> when the server reports the commit as done; otherwise
	/// <c>Error</c> holds the RPC error, the server's <c>message_exception</c>, or a generic message.
	/// </returns>
	/// <remarks>
	/// Calls <c>hp.pdm.commit.start_commit</c> with this record's id and copies <c>committed</c>, <c>errored</c>
	/// and <c>message_exception</c> from the first returned row into this object.
	/// </remarks>
	public async Task<(bool Committed, string? Error)> ServerCommit()
	{
		Result<IReadOnlyList<OdooRow>> response = await OdooRpc.ExecuteAsync<IReadOnlyList<OdooRow>>(GetHpModel()!, "start_commit", [this.id]);
		if (!response.Ok)
			return (false, response.Error);
		if (response.Value is [var row, ..])
		{
			bool committed = row.Bool("committed");
			this.committed = committed;
			errored = row.Bool("errored");
			message_exception = row.Text("message_exception");
			return committed ? (true, null) : (false, message_exception ?? "the server did not complete the commit");
		}
		return (false, "the server did not complete the commit");
	}
	/// <summary>Asks the server to clear this commit (discard its staged records).</summary>
	/// <returns><see langword="true"/> when the server reports success; <see langword="false"/> when it doesn't or the call failed.</returns>
	/// <remarks>Calls <c>hp.pdm.commit.clear_commit</c> with this record's id; the error of a failed call is discarded.</remarks>
	public async Task<bool> ServerClear()
		=> (await OdooRpc.ExecuteAsync<bool>(GetHpModel()!, "clear_commit", [this.id])).ValueOr(false);
}
