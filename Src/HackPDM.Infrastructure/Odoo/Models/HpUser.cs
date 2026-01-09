using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using HackPDM.Core.General;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;

using Windows.Media.Protection.PlayReady;

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.RES_USERS_NAME, OdooDefaultsConstants.RES_USERS)]
public partial class HpUser : HpBaseModelTransport<HpUser>, IHpUserModel
{
	[OdooProp(OdooFieldType.Char)] public string? name { get; set; }
	[OdooProp(OdooFieldType.Char)] public string? login { get; set; }
	[OdooProp(OdooFieldType.Char)] public string? email { get; set; }
	[OdooProp(OdooFieldType.Char)] public string? signature { get; set; }

	[OdooProp(OdooFieldType.DateTime)] public DateTime? login_date { get; set; }

	[OdooProp(OdooFieldType.Boolean)] public bool? active { get; set; }

	[OdooProp(OdooFieldType.Many2one)] public int? company_id { get; set; }
}