using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;

using HackPDM.Domain.Representation;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;
using HackPDM.UI.Types;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

using SolidWorks.Interop.swdocumentmgr;

using Windows.Foundation;
using Windows.UI;

using EntryRow = HackPDM.UI.Types.EntryRow;
using FlowDirection = HackPDM.Shared.GlobalData.FlowDirection;
using TreeData = HackPDM.UI.Types.TreeData;

namespace HackPDM.UI.Forms.Helper;

public static class FormHelper
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
                MessageBox.ShowAsync("Path is a null reference.  Could not find its parent.",
                    "Path Error",
                    buttons: MessageBoxButtons.OK,
                    icon: MessageBoxIcon.Error);
                return ("");
            }
            catch (ArgumentException)
            {
                MessageBox.ShowAsync("Path is an empty string.  Could not find its parent.",
                    "Path Error",
                    buttons: MessageBoxButtons.OK,
                    icon: MessageBoxIcon.Error);
                return ("");
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                MessageBox.ShowAsync("The parent directory for path \"" + path + "\" could not be found.",
                    "Path Error",
                    buttons: MessageBoxButtons.OK,
                    icon: MessageBoxIcon.Error);
                return ("");
            }
            catch
            {
                MessageBox.ShowAsync("Could not find the parent directory for \"" + path + "\".",
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
            MessageBox.ShowAsync("Error getting Base Name from \"" + path + "\".",
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
            MessageBox.ShowAsync("Error finding local files.",
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
    extension(FrameworkElement element)
    {
		public void SetGrid( int row, int column, int rowspan = 1, int columnspan = 1 )
		{
            element.SetValue( Grid.RowProperty, row );
			element.SetValue( Grid.ColumnProperty, column );
			element.SetValue( Grid.RowSpanProperty, rowspan );
			element.SetValue( Grid.ColumnSpanProperty, columnspan );
		}
	}
    
    public static LinearGradientBrush EZGradient(FlowDirection direction, params Color[] colorGradient)
    {
        return EZGradient( direction switch
        {
            FlowDirection.TopToBottom => 90,
            FlowDirection.TopLeftToBottomRight => 135,
            FlowDirection.BottomLeftToTopRight => 225,
            _ => 0,
        }, colorGradient );
    }
    public static ref Color ChangeColor(this ref Color color, byte? A = null, byte? R = null, byte? G = null, byte? B = null)
    {
        if (A is { } a) color.A = a;
		if (R is { } r) color.R = r;
		if (G is { } g) color.G = g;
		if (B is { } b) color.B = b;
        return ref color;
	}
    public static Color ModifyColor(this Color color, byte? A = null, byte? R = null, byte? G = null, byte? B = null)
	{
		if (A is { } a) color.A = a;
		if (R is { } r) color.R = r;
		if (G is { } g) color.G = g;
		if (B is { } b) color.B = b;
		return color;
	}
	// angle in degrees mod 360
	public static LinearGradientBrush EZGradient( double angle, params Color[] colorGradient )
    {
		var gradStopCollection = new GradientStopCollection();

		for (int i = 0; i < colorGradient.Length; i++ )
		{
			var gradStop1 = new GradientStop
			{
				Color = colorGradient[i],
                Offset = ((double)i / (colorGradient.Length - 1))
			};
			gradStopCollection.Add( gradStop1 );
		}
		var linGradient = new LinearGradientBrush(gradStopCollection, angle);
		return linGradient;
	}
    public static RadialGradientBrush EZRadGradient(Vector4<double> circView, Vector2<double> gradCenter, double opacity = 1, params Color[] colorGradient)
    {
		var radGradient = new RadialGradientBrush
		{
			SpreadMethod = GradientSpreadMethod.Pad,
            Center = new Point(circView.x, circView.y),
			RadiusX = circView.z,
			RadiusY = circView.w,
            GradientOrigin = new Point(gradCenter.x, gradCenter.y),
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
            InterpolationSpace = Microsoft.UI.Composition.CompositionColorSpace.Auto,
            Opacity = opacity,
		};
        foreach (var gradStop in EvenSpacedGradient(colorGradient))
        {
            radGradient.GradientStops.Add(gradStop);
        }
		return radGradient;
	}
    public static GradientStopCollection EvenSpacedGradientCollection(params Color[] colorGradient)
    {
		var gradStopCollection = new GradientStopCollection();
        foreach (var gradStop in EvenSpacedGradient(colorGradient))
        {
			gradStopCollection.Add( gradStop );
		}
        return gradStopCollection;
	}
    public static IEnumerable<GradientStop> EvenSpacedGradient(params Color[] colorGradient)
    {
		for( int i = 0; i < colorGradient.Length; i++ )
		{
			var gradStop1 = new GradientStop
			{
				Color = colorGradient[i],
				Offset = (i / (colorGradient.Length - 1))
			};
            yield return gradStop1;
		}
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
public partial class FileSizeConverter : IValueConverter
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
