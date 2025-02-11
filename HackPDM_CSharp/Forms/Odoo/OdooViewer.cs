using CredentialManagement;
using OdooObjects;
using OdooRpcCs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HackPDM
{
    public partial class OdooViewer : Form
    {
        
        private OdooTools oTools;

        private TreeView model_view;
        private TreeView model_instances;
        public OdooViewer()
        {
            InitializeComponent();
            SetTreeViews();
            
            oTools = new();

            debugTest();
            
        }
        private void SetTreeViews()
        {
            this.model_view = new System.Windows.Forms.TreeView();
            this.model_instances = new System.Windows.Forms.TreeView();

            this.tabPage1.Controls.Add(this.model_view);
            this.tabPage2.Controls.Add(this.model_instances);

            this.model_view.Dock = DockStyle.Fill;
            this.model_instances.Dock = DockStyle.Fill;
        }


        private void debugTest()
        {
            List<string> allModels = oTools.GetAllModels();
            
            model_viewer.BeginUpdate();
            foreach (var model in allModels)
            {
                model_viewer.Nodes.Add(model);
            }
            model_viewer.EndUpdate();
        }

        private void model_viewer_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            // get all instances for model
            // oTools.TestMethods();

            // get all fields for model
            Hashtable entries = oTools.GetAllFieldsForModel(e.Node.Text);
            AddEntriesToTree(model_view, e.Node.Text, entries);
            ArrayList instances = oTools.GetModelInstances(e.Node.Text);
            AddEntriesToTree(model_instances, e.Node.Text, instances);
        }
        private void AddEntriesToTree(TreeView tree, string modelName, ArrayList entries)
        {
            tree.Nodes.Clear();
            
            if (entries == null || entries.Count == 0)
            {
                tree.Nodes.Add(modelName + " | No Instances");
                return;
            }
            TreeNode topNode = tree.Nodes.Add(modelName);
            tree.ExpandAll();
            Hashtable newTable = [];
            for (int i = 0; i < entries.Count; i++)
            {
                newTable.Add($"{i}", entries[i]);
            }
            // tree.TopNode doesn't update fast enough before it enters the function so it was assigned a variable topNode
            RecurseAddNodes(topNode, newTable, 0);
            //SortNodesByRequired(tree);
            tree.Sort();
        }

        private void AddEntriesToTree(TreeView tree, string modelName, Hashtable entries)
        {
            tree.Nodes.Clear();
            TreeNode topNode = tree.Nodes.Add(modelName);
            tree.ExpandAll();
            // tree.TopNode doesn't update fast enough before it enters the function so it was assigned a variable topNode
            RecurseAddNodes(topNode, entries, 0);
            SortNodesByRequired(tree);
        }
        private void RecurseAddNodes(TreeNode treeNode, Hashtable node, int depth)
        {
            bool wasFound = SearchKeys(node, "required", out bool isRequired);
            if (wasFound && isRequired)
            {
                treeNode.ForeColor = Color.Red;
            }
            else
            {
                treeNode.ForeColor = Color.Black;
            }

            foreach (DictionaryEntry pair in node)
            {
                TreeNode newTreeNode = treeNode.Nodes.Add(pair.Key.ToString());

                if (pair.Value is Hashtable newTable)
                {
                    RecurseAddNodes(newTreeNode, newTable, depth+1);
                }
                else
                {
                    if (pair.Value == null)
                    {
                        newTreeNode.Nodes.Add("null");
                    }
                    else
                    {
                        TreeNode newNode = newTreeNode.Nodes.Add(pair.Value.ToString());

                        if (pair.Value is ArrayList arr)
                        {
                            foreach (var value in arr)
                            {
                                newNode.Nodes.Add(value.ToString());
                            }
                        }
                    }
                }
            }
        }
        private void SortNodesByRequired(TreeView tree)
        {
            IComparer oldComparer = tree.TreeViewNodeSorter;
            tree.TreeViewNodeSorter = new NodeSorter();
            tree.Sort();
            tree.TreeViewNodeSorter = oldComparer;
        }

        private void tabPage1_Click(object sender, EventArgs e)
        {

        }

        internal static bool SearchChildrenNodeText(TreeNodeCollection tree, string matchText, out TreeNode tn)
        {
            foreach (TreeNode node in tree)
            {
                if (node.Text.Trim() == matchText)
                {
                    tn = node;
                    return true;
                }
            }
            tn = null;
            return false;
        }
        internal static bool SearchKeys<T>(Hashtable node, string matchText, out T value)
        {
            if (node.ContainsKey(matchText))
            {
                value = (T)node[matchText];
                return true;
            }
            value = default(T);
            return false;
        }

        private void StatusBox_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
    public class NodeSorter : IComparer
    {
        public int Compare(object x, object y)
        {
            TreeNode tx = x as TreeNode;
            TreeNode ty = y as TreeNode;

            bool txRequired = false;
            bool tyRequired = false;

            if (tx.Text == "partner_id")
            {
                txRequired = false;
            }
            //TreeNode txReqNode;
            //TreeNode tyReqNode;
            bool txContains = OdooViewer.SearchChildrenNodeText(tx.Nodes, "required", out TreeNode txReqNode);
            bool tyContains = OdooViewer.SearchChildrenNodeText(ty.Nodes, "required", out TreeNode tyReqNode);

            if (txContains)
            {
                string txReqValue = txReqNode.FirstNode.Text.Trim();
                txRequired = txReqValue == "True";
            }
            if (tyContains)
            {
                string tyReqValue = tyReqNode.FirstNode.Text.Trim();
                tyRequired = tyReqValue == "True";                
            }

            if (txRequired && !tyRequired) return -1;
            if (tyRequired && !txRequired) return 1;
            return string.Compare(tx.Text.Trim(), ty.Text.Trim());
        }
    }
}
