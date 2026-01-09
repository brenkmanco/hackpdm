using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;
using HackPDM.UI.Types;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace HackPDM.UI.Forms.Helper;

internal static class FormHelper
{
    /// <summary>
    /// Returns an absolute or relative path for the parent of the passed argument
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    /// 
    public static string GetParentDirectory(string path)
    {
        // Check if path is a relative or absolute path:
        if (System.IO.Path.IsPathRooted(path))
        {
            // This is an absolute path:
            try
            {
                System.IO.DirectoryInfo directoryInfo = System.IO.Directory.GetParent(path);
                return (directoryInfo.FullName);
            }
            catch (ArgumentNullException)
            {
                MessageBox.Show("Path is a null reference.  Could not find its parent.",
                    "Path Error",
                    buttons: MessageBoxButtons.OK,
                    icon: MessageBoxIcon.Error);
                return ("");
            }
            catch (ArgumentException)
            {
                MessageBox.Show("Path is an empty string.  Could not find its parent.",
                    "Path Error",
                    buttons: MessageBoxButtons.OK,
                    icon: MessageBoxIcon.Error);
                return ("");
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                MessageBox.Show("The parent directory for path \"" + path + "\" could not be found.",
                    "Path Error",
                    buttons: MessageBoxButtons.OK,
                    icon: MessageBoxIcon.Error);
                return ("");
            }
            catch
            {
                MessageBox.Show("Could not find the parent directory for \"" + path + "\".",
                    "Path Error",
                    buttons: MessageBoxButtons.OK,
                    icon: MessageBoxIcon.Error);
                return ("");
            }
        }
        else
        {
            // This is a relative path.  Check if there are any slashes:
            if (path.Contains("\\"))
            {
                return (path.Substring(0, path.LastIndexOf("\\")));
            }
            else
            {
                // This is the last parent directory
                // TODO: Correct code to be more consisent (Some code may expect this method to return "pwa")
                // Return the empty string:
                return ("");
            }
        }
    }
    public static string GetBaseName(string path)
    {
        try
        {
            return (System.IO.Path.GetFileName(path));
        }
        catch
        {
            MessageBox.Show("Error getting Base Name from \"" + path + "\".",
                "Path Error",
                buttons: MessageBoxButtons.OK,
                icon: MessageBoxIcon.Error);
            return ("");
        }
    }
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
        catch ( System.Exception )
        {
            MessageBox.Show("Error finding local files.",
                "File Discovery Error",
                buttons: MessageBoxButtons.OK,
                icon: MessageBoxIcon.Error);
        }
    }


    public static (int, TreeViewNode?) LastValidTreeIndex(in string combinedPath, in string[] paths, in Dictionary<string, TreeViewNode> nodeMap)
    {
        ReadOnlySpan<char> strArray = combinedPath.AsSpan();
        int pathLength = combinedPath.Length;

        for (int i = paths.Length - 1; i >= 0; i--)
        {
            if (nodeMap.TryGetValue(strArray.Slice(0, pathLength).ToString(), out TreeViewNode? node))
            {
                return (i, node);
            }
            pathLength -= paths[i].Length + 1;
        }
        return (-1, null);
    }
    private static (int, TreeViewNode?) RecurseNodePath(in TreeViewNode currentNode, string[] nodes, int index)
    {
        if (currentNode == null)
        { 
            return (index-1, null); 
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
    public static Dictionary<string, TreeViewNode>? ConvertTreeToDictionary(in TreeView tree)
    {
        if (tree.RootNodes.Count == 0) return null;

        Dictionary<string, TreeViewNode> treeDictionary = [];
        foreach (var node in tree.RootNodes) RecurseNodesConvert(node, in treeDictionary);

        return treeDictionary;
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

    extension(DispatcherQueue dispatcher)
    {
        internal async Task ExecuteUI(Func<Task> function)
        {
            if (dispatcher.HasThreadAccess)
            {
                await function();
            }
            else
            {
                dispatcher.TryEnqueue(async void ()=>
                {
                    try
                    {
                        await function();
                    }
                    catch (Exception e)
                    {
                        Debug.Fail(e.Message, e.StackTrace);
                    }
                });
            }
        }

        internal void ExecuteUI(Action function)
        {
            if (dispatcher.HasThreadAccess)
            {
                function();
            }
            else
            {
                dispatcher.TryEnqueue(()=>function());
            }
        }

        internal Task ExecuteUIAsync(Action function)
            => Task.Run(async () => ExecuteUI(dispatcher, function));
    }

    static ScrollViewer? GetScrollViewer(DependencyObject parent)
	{
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
		{
			var child = VisualTreeHelper.GetChild(parent, i);
			if (child is ScrollViewer sv)
				return sv;

			var result = GetScrollViewer(child);
			if (result != null)
				return result;
		}
		return null;
	}
}
public class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is long bytesize) 
        {
            return bytesize switch 
            { 
                < 1024 => $"{bytesize}     B",
                < 1048576 => $"{bytesize / 1024f:.##}   KB",
                < 1073741824 => $"{bytesize / 1048576f:.##}   MB",
                < 1099511627776 => $"{bytesize / 1073741824f:.##}   GB",
                <= 1125899906842624 => $"{bytesize / 1099511627776f:.##}   TB",
                _ => $"{bytesize}     B",
            };
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value;
    }		
}
