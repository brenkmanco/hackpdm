using System.Collections;
using HackPDM.Extensions.General;
//using static System.Net.Mime.MediaTypeNames;


using OClient = HackPDM.Odoo.OdooClient;
// ReSharper disable InconsistentNaming

namespace HackPDM.Odoo.OdooModels.Models;

[OdooModel(OdooDefaults.HP_NODE_NAME, OdooDefaults.HP_NODE)]
public partial class HpNode : HpBaseModel<HpNode>
{
	[OdooField(OdooFieldType.Char)] public string name;
    
        public HpNode() { }
        public HpNode(
            string name)
        {
            this.name = name;
        }
}
public partial class HpNode : HpBaseModel<HpNode>
{
    internal void UpdateNodeLatestVersions(int[] versionIds)
    {
        OClient.Command<ArrayList>(GetHpModel(), "update_node_latest_versions", [versionIds.ToArrayList()], 1000000);
    }
    public override string ToString() 
    {
        return name;
    }
}