using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Infrastructure.SldWrks;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

// Resharper disable InconsistentNaming


//using static System.Net.Mime.MediaTypeNames;



namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_VERSION_RELATIONSHIP_NAME, OdooDefaultsConstants.HP_VERSION_RELATIONSHIP)]
public partial class HpVersionRelationship : HpBaseModelTransport<HpVersionRelationship>, IHpVersionRelationshipModel
{
    [OdooProp(OdooFieldType.Many2One, "parent_id")] public Many2One? parent_id { get; set; }
	[OdooProp(OdooFieldType.Many2One, "child_id")] public Many2One? child_id { get; set; }
	IMany2One? IHpVersionRelationshipModel.parent_id { get => (IMany2One?)parent_id; set => parent_id = (Many2One?)value; }
	IMany2One? IHpVersionRelationshipModel.child_id { get => (IMany2One?)child_id; set => child_id = (Many2One?)value; }

	public HpVersionRelationship() { } 
    public HpVersionRelationship(
        int parentId = 0,
        int childId = 0)
    {
        this.parent_id = parentId;
        this.child_id = childId;
    }
}
