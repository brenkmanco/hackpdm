//using static System.Net.Mime.MediaTypeNames;

using System;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

// Resharper disable InconsistentNaming

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_RELEASE_NAME, OdooDefaultsConstants.HP_RELEASE)]
public interface IHpReleaseModel : IHpOdooRecord
{
    [OdooProp(OdooFieldType.Many2one, "version_id")] public IMany2One? version_id { get; set; }
    [OdooProp(OdooFieldType.Many2one, "release_user_id")] public IMany2One? release_user_id {get;set;}
	[OdooProp(OdooFieldType.DateTime, "release_stamp")] public DateTime? release_stamp {get;set;}
	[OdooProp(OdooFieldType.Char, "release_note")] public string? release_note {get;set;}
}