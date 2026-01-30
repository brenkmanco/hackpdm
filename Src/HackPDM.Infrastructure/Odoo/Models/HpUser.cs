using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using HackPDM.Core.General;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

using Windows.Media.Protection.PlayReady;

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.RES_USERS_NAME, OdooDefaultsConstants.RES_USERS)]
public partial class HpUser : HpBaseModelTransport<HpUser>, IHpUserModel
{
	[OdooProp(OdooFieldType.Char, "name")] public string? name { get; set; }
	[OdooProp(OdooFieldType.Char, "login")] public string? login { get; set; }
	[OdooProp(OdooFieldType.Char, "email")] public string? email { get; set; }
	[OdooProp(OdooFieldType.Char, "signature")] public string? signature { get; set; }

	[OdooProp(OdooFieldType.DateTime, "login_date")] public DateTime? login_date { get; set; }

	[OdooProp(OdooFieldType.Boolean, "active")] public bool? active { get; set; }

	[OdooProp(OdooFieldType.Many2One, "company_id")] public Many2One? company_id { get; set; }
	IMany2One? IHpUserModel.company_id { get =>(IMany2One?)company_id; set => company_id = (Many2One?)value; }
}