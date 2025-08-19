using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

using HackPDM.ClientUtils;

using Theme = HackPDM.ClientUtils.Theme;

namespace HackPDM.Extensions.Form
{
    public static class ExtensionForm
    {
        public static bool SetFormTheme(this Control control, Theme theme, bool isRoot = true)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (theme == null) throw new ArgumentNullException(nameof(theme));
            try
            {
                Debug.WriteLine($"c name: {control.Name}");
                control.BackColor = isRoot ? theme.BackgroundColor ?? Color.White : theme.SecondaryBackgroundColor ?? Color.LightGray;
                control.ForeColor = theme.ForegroundColor ?? Color.Black;
                control.Font = new Font(theme.FontFamily, theme.FontSize);

                foreach (Control item in control.Controls)
                {
                    item.SetFormTheme(theme, false);
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.Fail($"Failed to set theme on form: {ex.Message}");
                return false;
            }
        }
        public static TreeNode FindTreeNode(this TreeView view, string path)
        {
            TreeNodeCollection nodes = null;
            TreeNode node = null;
            string[] paths = path.Split('\\');
            try
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    if (i == 0)
                        nodes = view.Nodes;
                    else
                        nodes = node.Nodes;

                    bool wasFound = false;
                    foreach (TreeNode n in nodes)
                    {
                        if (n.Text == paths[i])
                        {
                            wasFound = true;
                            node = n;
                            break;
                        }
                    }
                    if (!wasFound)
                        return null;
                }
                return node;
            }
            catch
            {
                return null;
            }
        }
    }
}
