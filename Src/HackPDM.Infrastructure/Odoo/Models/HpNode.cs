using System.Collections;
using HackPDM.Core.General;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Shared.GlobalData;
using OClient = HackPDM.Infrastructure.Odoo.OdooClient;

// Resharper disable InconsistentNaming

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_NODE_NAME, OdooDefaultsConstants.HP_NODE)]
public partial class HpNode : HpBaseModelTransport<HpNode>, IHpNodeModel
{
	[OdooProp(OdooFieldType.Char)] public string name { get; set; }

	internal static void UpdateNodeLatestVersions(int[] versionIds)
    {
        OClient.Command<ArrayList>(GetHpModel(), "update_node_latest_versions", [versionIds.ToArrayList()], 1000000);
    }
}