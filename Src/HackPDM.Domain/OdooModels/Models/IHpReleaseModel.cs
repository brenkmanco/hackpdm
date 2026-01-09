//using static System.Net.Mime.MediaTypeNames;

using System;
using HackPDM.Shared.GlobalData;

// Resharper disable InconsistentNaming

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_RELEASE_NAME, OdooDefaultsConstants.HP_RELEASE)]
public interface IHpReleaseModel : IHpOdooRecord
{
    [OdooProp(OdooFieldType.Many2one)] public int version_id { get; set; }
    [OdooProp(OdooFieldType.Many2one)] public int release_user_id {get;set;}
	[OdooProp(OdooFieldType.DateTime)] public DateTime? release_stamp {get;set;}
	[OdooProp(OdooFieldType.Char)] public string release_note {get;set;}
}