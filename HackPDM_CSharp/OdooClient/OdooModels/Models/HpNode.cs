using System.Collections;

using HackPDM.Extensions.General;


//using static System.Net.Mime.MediaTypeNames;


using OClient = OdooRpcCs.OdooClient;

namespace HackPDM
{
    public class HpNode : HpBaseModel<HpNode>
    {
        public string name;

        public HpNode() { }
        public HpNode(
            string name)
        {
            this.name = name;
        }
        internal void UpdateNodeLatestVersions(int[] version_ids)
        {
            OClient.Command<ArrayList>(GetHpModel(), "update_node_latest_versions", [version_ids.ToArrayList()], 1000000);
        }
		public override string ToString() 
        {
            return name;
        }
	}
}
