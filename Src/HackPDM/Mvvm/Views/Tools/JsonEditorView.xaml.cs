using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using HackPDM.Infrastructure.Odoo;
using HackPDM.Mvvm.ViewModels.Tools;
using HackPDM.Mvvm.Views.Common.Settings;

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;

using static System.Runtime.InteropServices.JavaScript.JSType;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM.Mvvm.Views.Tools;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class JsonEditorView : Page
{
	public ObservableCollection<JsonNodeViewModel> RootNodes { get; } = [];
	public JsonEditorView()
	{
		InitializeComponent();
	}
	private void OnLoadJsonClick(object sender, RoutedEventArgs e)
	{
		string sampleJson = """
        {
          "appName": "Automation Engine",
          "maxRetries": 5,
          "isProduction": true,
          "dbConfig": {
            "host": "localhost",
            "port": 5432
          },
          "allowedPorts": [80, 443, 8080]
        }
        """;
		JsonNode? jsonNode = null;
		if (File.Exists(GlobalSerializedData.FilePath))
		{
			string json = File.ReadAllText(GlobalSerializedData.FilePath);
			jsonNode = JsonNode.Parse(json);
		}
		jsonNode ??= JsonNode.Parse(sampleJson);

		RootNodes.Clear();

		if (jsonNode is JsonObject obj)
		{
			foreach (var kvp in obj)
			{
				RootNodes.Add(new JsonNodeViewModel(kvp.Key, kvp.Value));
			}
		}
	}
	private JsonObject? GetRootObject()
	{
		var rootObj = new JsonObject();
		foreach( var node in RootNodes ) {
			rootObj[ node.Key ] = node.ToJsonNode();
		}
		return rootObj;
	}
	private async void OnExportJsonClick( object sender, RoutedEventArgs e )
	{
		var rootObj = GetRootObject() ?? [];
		string exportedJson = rootObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

		ContentDialog dialog = new() {
			Title = "Exported JSON Output",
			Content = new ScrollViewer
			{
				Content = new TextBlock
				{
					Text = exportedJson,
					IsTextSelectionEnabled = true,
					FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
				},
				MaxHeight = 400
			},
			PrimaryButtonText = "Save",
			CloseButtonText = "Close",
			XamlRoot = this.XamlRoot
		};
		switch( await dialog.ShowAsync() ) {
			case ContentDialogResult.Primary:
			GlobalSerializedData.LoadFromJson( exportedJson ).SaveToFile();
			break;
			case ContentDialogResult.Secondary:
			case ContentDialogResult.None:
			break;
		}
	}

	private void OnAddChildClick(object sender, RoutedEventArgs e)
	{
		if (sender is Button { DataContext: JsonNodeViewModel vm })
		{
			vm.AddChildProperty();
		}
	}

	private void OnDeleteNodeClick(object sender, RoutedEventArgs e)
	{
		if (sender is Button { DataContext: JsonNodeViewModel vm })
		{
			if (vm.Parent != null)
			{
				vm.DeleteSelf();
			}
			else
			{
				RootNodes.Remove(vm);
			}
		}
	}

	// Recalculates TreeViewItemIndent as items expand/collapse to keep deep trees on-screen
	private void UpdateDynamicIndent()
	{
		int maxExpandedDepth = GetMaxExpandedDepth(RootNodes, 0);

		// Scale indent step ratio dynamically: compresses step width as max opened depth increases
		double calculatedIndent = Math.Max(18.0, Math.Min(48.0, 320.0 / (maxExpandedDepth + 1)));

		// Override the TreeView runtime resource dictionary value
		JsonTreeView.Resources["TreeViewItemIndent"] = calculatedIndent;
	}

	private static int GetMaxExpandedDepth(IEnumerable<JsonNodeViewModel> nodes, int currentDepth)
	{
		int max = currentDepth;
		foreach (var node in nodes)
		{
			if (node.IsContainer && node.IsExpanded && node.Children.Count > 0)
			{
				max = Math.Max(max, GetMaxExpandedDepth( node.Children, currentDepth + 1));
			}
		}
		return max;
	}
	private bool _isCodeView;

	private void OnViewModeChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
	{
		if (sender.SelectedItem != null)
		{
			_isCodeView = sender.SelectedItem.Tag?.ToString() == "Code";

			if (_isCodeView)
			{
				// Sync Visual Tree -> Code Editor
				SyncTreeToCodeView();
				VisualTreeBorder.Visibility = Visibility.Collapsed;
				CodeEditorGrid.Visibility = Visibility.Visible;
			}
			else
			{
				// Sync Code Editor -> Visual Tree
				if (SyncCodeViewToTree())
				{
					CodeEditorGrid.Visibility = Visibility.Collapsed;
					VisualTreeBorder.Visibility = Visibility.Visible;
					JsonParseInfoBar.IsOpen = false;
				}
				else
				{
					// Revert selection back to Code IDE if JSON parse failed
					sender.SelectedItem = sender.Items[1];
				}
			}
		}
	}

	private void SyncTreeToCodeView()
	{
		var rootObj = GetRootObject() ?? [];

		string rawJson = rootObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
		ApplyJsonSyntaxHighlighting(rawJson);
	}

	private bool SyncCodeViewToTree()
	{
		CodeEditor.Document.GetText(TextGetOptions.None, out string codeText);
		try
		{
			var jsonNode = JsonNode.Parse(codeText);
			RootNodes.Clear();

			if (jsonNode is JsonObject obj)
			{
				foreach (var kvp in obj)
				{
					RootNodes.Add(new JsonNodeViewModel(kvp.Key, kvp.Value));
				}
			}
			return true;
		}
		catch (JsonException ex)
		{
			JsonParseInfoBar.Message = $"JSON Syntax Error: {ex.Message}";
			JsonParseInfoBar.IsOpen = true;
			return false;
		}
	}
	private void ApplyJsonSyntaxHighlighting(string jsonText)
	{
		// Fix index drift: Normalize newlines to match RichEditBox's internal 1-character format (\r)
		string normalizedJson = jsonText.Replace("\r\n", "\r");

		var doc = CodeEditor.Document;
		doc.SetText(TextSetOptions.None, normalizedJson);

		// Dark-Theme IDE Color Tokens
		Color colorKey = Color.FromArgb(255, 156, 220, 254);     // Light Blue (#9CDCFE)
		Color colorString = Color.FromArgb(255, 206, 145, 120);  // Terra Cotta / Amber (#CE9178)
		Color colorNumber = Color.FromArgb(255, 181, 206, 168);  // Pale Green (#B5CEA8)
		Color colorKeyword = Color.FromArgb(255, 86, 156, 214);  // Deep Blue / Keyword (#569CD6)
		Color colorPunct = Color.FromArgb(255, 212, 212, 212);   // Off-White Punctuation (#D4D4D4)

		// 1. Base Punctuation Tint
		ITextRange fullRange = doc.GetRange(0, normalizedJson.Length);
		fullRange.CharacterFormat.ForegroundColor = colorPunct;

		// 2. Tokenize & Colorize Patterns using the normalized string
		void ColorizePattern(string pattern, Color color)
		{
			foreach (Match match in Regex.Matches(normalizedJson, pattern))
			{
				ITextRange range = doc.GetRange(match.Index, match.Index + match.Length);
				range.CharacterFormat.ForegroundColor = color;
			}
		}

		ColorizePattern(@"""[^""\\]*(?:\\.[^""\\]*)*""(?=\s*:)", colorKey);      // JSON Keys
		ColorizePattern(@"(?<=:\s*)""[^""\\]*(?:\\.[^""\\]*)*""", colorString);  // String Values
		ColorizePattern(@"\b-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b", colorNumber);    // Numbers
		ColorizePattern(@"\b(true|false|null)\b", colorKeyword);                  // Booleans & Null
	}
}
