global using HackPDM.UI.Helpers;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Types;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

using EntryRow = HackPDM.UI.Types.EntryRow;
using FlowDirection = HackPDM.Shared.GlobalData.FlowDirection;
using TreeData = HackPDM.UI.Types.TreeData;

namespace HackPDM.UI.Forms.Helper;

public static class FormHelper
{
    public static void GetAllFilesInDir(string dirpath, ref List<string> filesfound)
    {
        try
        {
            foreach (string d in Directory.GetDirectories(dirpath))
            {
                foreach (string f in Directory.GetFiles(d))
                {
                    filesfound.Add(f);
                }

                GetAllFilesInDir(d, ref filesfound);
            }
        }
        catch (System.Exception)
        {
            MessageBox.ShowAsync("Error finding local files.",
                "File Discovery Error",
                buttons: MessageBoxButtons.OK,
                icon: MessageBoxIcon.Error);
        }
    }

    private static (int, TreeViewNode?) RecurseNodePath(in TreeViewNode currentNode, string[] nodes, int index)
    {
        if (currentNode == null)
        { 
            return (index - 1, null); 
        }
        var entry = currentNode.Content as EntryRow;
        if (index >= nodes.Length || entry?.Name != nodes[index])
        {
            return (index - 1, currentNode);
        }
        if (index == nodes.Length - 1)
        {
            return (index, currentNode);
        }
            
        if (entry.Name == nodes[index]) 
        { 
            foreach (TreeViewNode child in currentNode.Children) 
            {
                var result = RecurseNodePath(child, nodes, index + 1);
                if (result.Item1 != index) 
                { 
                    return result; 
                } 
            } 
        }
        return (index, currentNode);
    }

    private static void RecurseNodesConvert(in TreeViewNode node, in Dictionary<string, TreeViewNode>? nodeMap)
    {
        var content = node?.Content as TreeData;
            
        nodeMap?.Add(content?.FullPath, node);
            
        foreach (TreeViewNode child in node.Children)
        {
            RecurseNodesConvert(in child, in nodeMap);
        }
    }

    // Delegations to dedicated helpers for backward compatibility
    public static LinearGradientBrush EZGradient(FlowDirection direction, params Color[] colorGradient)
        => ColorDrawingHelper.EZGradient(direction, colorGradient);

    public static LinearGradientBrush EZGradient(double angle, params Color[] colorGradient)
        => ColorDrawingHelper.EZGradient(angle, colorGradient);

    public static IEnumerable<GradientStop> EvenSpacedGradient(params Color[] colorGradient)
        => ColorDrawingHelper.EvenSpacedGradient(colorGradient);

    public static ScrollViewer? GetScrollViewer(DependencyObject parent)
        => VisualTreeExtensions.GetScrollViewer(parent);
}
