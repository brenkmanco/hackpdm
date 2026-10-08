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
using HackPDM.UI.Data;

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

namespace HackPDM.UI.Forms.Helper;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class DynamicJsonForm : Page
{
	public ObservableCollection<JsonNodeViewModel> RootNodes { get; } = [];
	public DynamicJsonForm()
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

	private async void OnExportJsonClick(object sender, RoutedEventArgs e)
	{
		var rootObj = new JsonObject();
		foreach (var node in RootNodes)
		{
			rootObj[node.Key] = node.ToJsonNode();
		}

		string exportedJson = rootObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

		ContentDialog dialog = new ContentDialog
		{
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
			CloseButtonText = "Close",
			XamlRoot = this.XamlRoot
		};

		await dialog.ShowAsync();
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

	private int GetMaxExpandedDepth(IEnumerable<JsonNodeViewModel> nodes, int currentDepth)
	{
		int max = currentDepth;
		foreach (var node in nodes)
		{
			if (node.IsContainer && node.IsExpanded && node.Children.Count > 0)
			{
				max = Math.Max(max, GetMaxExpandedDepth(node.Children, currentDepth + 1));
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
		var rootObj = new JsonObject();
		foreach (var node in RootNodes)
		{
			rootObj[node.Key] = node.ToJsonNode();
		}

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
public partial class JsonNodeViewModel : INotifyPropertyChanged
{
	public static JsonNodeType[] AllNodeTypes { get; } = Enum.GetValues<JsonNodeType>();
	public string Key
	{
		get;
		set { field = value; OnPropertyChanged(); }
	}
	public string RawValue
	{
		get;
		set
		{
			field = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(BoolValue));
			OnPropertyChanged(nameof(NumberValue));
			Validate();
		}
	} = string.Empty;
	public int Depth { get; private set; }

	// Track expansion state to trigger active parent styling and indent recalculation
	public Action? OnExpandedChanged { get; set; }
	public bool IsExpanded
	{
		get;
		set
		{
			if (field != value)
			{
				field = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(CardBackgroundHex));
				OnExpandedChanged?.Invoke();
			}
		}
	}
	// Dark-mode optimized dark-pastel depth fills (prevents high-contrast text blinding)
	private static readonly string[] DarkPastelPalette = new[]
	{
		"#252A36", // Depth 0: Dark Ice Blue
		"#2E2638", // Depth 1: Dark Lavender
		"#213028", // Depth 2: Dark Mint
		"#362C20", // Depth 3: Dark Amber
		"#342226"  // Depth 4: Dark Rose
	};

	public string DepthColorHex => DarkPastelPalette[Depth % DarkPastelPalette.Length];

	// Highlight background color when parent node is expanded
	public string CardBackgroundHex => (IsContainer && IsExpanded && ChildCount > 0)
		? "#2B384E" // Active expanded parent highlight in dark mode
		: DepthColorHex;
	// Helper property for WinUI 3 boolean-to-visibility conversion
	public bool IsChildNode => Depth > 0;
	// Tree branch connector helpers
	public bool IsLastChild => Parent != null && Parent.Children.IndexOf(this) == Parent.Children.Count - 1;
	public bool HasNextSibling => !IsLastChild;
	// Generates index array [0..Depth-1] for rendering vertical left branch guide strips
	public int[] DepthStrips => [.. Enumerable.Range(0, Depth)];

	public JsonNodeType? NodeType
	{
		get;
		set
		{
			if (field != value)
			{
				field = value;
				OnPropertyChanged();

				if (IsContainer)
				{
					RawValue = string.Empty;
					if (field == JsonNodeType.Array)
					{
						ReindexArrayChildren();
					}
					else if (field == JsonNodeType.Object)
					{
						// Convert array keys [0], [1] to property keys
						for (int i = 0; i < Children.Count; i++)
						{
							if (Children[i].Key.StartsWith('[') && Children[i].Key.EndsWith(']'))
							{
								Children[i].Key = $"property_{i + 1}";
							}
						}
					}
				}
				else
				{
					// Clear child nodes when converting container to primitive
					Children.Clear();

					// Set sensible default values for primitives
					if (field == JsonNodeType.Boolean && !bool.TryParse(RawValue, out _))
						RawValue = "False";
					else if (field == JsonNodeType.Number && !double.TryParse(RawValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
						RawValue = "0";
					else if (field == JsonNodeType.Null)
						RawValue = "null";
				}

				NotifyTypeFlags();
				Validate();
			}
		}
	}

	public bool IsContainer => NodeType == JsonNodeType.Object || NodeType == JsonNodeType.Array;
	public bool IsObject => NodeType == JsonNodeType.Object;
	public bool IsArray => NodeType == JsonNodeType.Array;
	public bool IsString => NodeType == JsonNodeType.String;
	public bool IsNumber => NodeType == JsonNodeType.Number;
	public bool IsBoolean => NodeType == JsonNodeType.Boolean;
	public bool IsNull => NodeType == JsonNodeType.Null;
	public bool BoolValue
	{
		get => bool.TryParse(RawValue, out var b) && b;
		set => RawValue = value.ToString();
	}
	public double NumberValue
	{
		get => double.TryParse(RawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;
		set => RawValue = value.ToString(CultureInfo.InvariantCulture);
	}
	// Type Glyph Icons (Segoe Fluent Icons)
	public string TypeGlyph => NodeType switch
	{
		JsonNodeType.Object => "\uE8B9",  // Folder / Object
		JsonNodeType.Array => "\uE8A1",   // List
		JsonNodeType.String => "\uE8C8",  // Text
		JsonNodeType.Number => "\uE8EF",  // Hash / Number
		JsonNodeType.Boolean => "\uE8D3", // Toggle / Switch
		_ => "\uE9CE"                     // Null / Empty
	};
	public ObservableCollection<JsonNodeViewModel> Children { get; } = new();
	public int ChildCount => Children.Count;
	public bool HasError
	{
		get;
		private set { field = value; OnPropertyChanged(); }
	}

	public string ErrorMessage
	{
		get;
		private set { field = value; OnPropertyChanged(); }
	} = string.Empty;

	public JsonNodeViewModel? Parent { get; set; }

	public JsonNodeViewModel(string key, JsonNode? node, JsonNodeViewModel? parent = null, Action? onExpandedChanged = null)
	{
		Key = key;
		Parent = parent;
		OnExpandedChanged = onExpandedChanged;
		Depth = parent == null ? 0 : parent.Depth + 1;

		Children.CollectionChanged += (s, e) =>
		{
			OnPropertyChanged(nameof(Children));
			OnPropertyChanged(nameof(ChildCount));
		};
		switch (node?.GetValueKind())
		{
			case null:
			case JsonValueKind.Undefined:
			case JsonValueKind.Null: 
			{
				NodeType = JsonNodeType.Null;
				RawValue = "null";
				break; 
			}
			case JsonValueKind.String:
			{
				NodeType = JsonNodeType.String;
				RawValue = TryHelper.TryGet(() => node.GetValue<string>(), out string? val) ? val ?? "" : "";
				break;
			}
			case JsonValueKind.Number:
			{
				NodeType = JsonNodeType.Number;
				RawValue = TryHelper.TryGet(()=>node.GetValue<double>(), out double? val) ? val?.ToString() ?? "" : "";
				break;
			}
			case JsonValueKind.True:
			case JsonValueKind.False:
			{
				NodeType = JsonNodeType.Boolean;
				RawValue = TryHelper.TryGet(() => node.GetValue<bool>(), out bool? val) ? val?.ToString() ?? "" : "";
				break;
			}
			case JsonValueKind.Object:
			{
				if (TryHelper.TryGet(() => node.AsObject(), out JsonObject? test) && test is JsonObject myVal)
				{
					NodeType = JsonNodeType.Object;
					foreach (var kvp in myVal)
					{
						Children.Add(new JsonNodeViewModel(kvp.Key, kvp.Value, this));
					}
				}
				else
				{
					if (node is JsonObject obj)
					{
						NodeType = JsonNodeType.Object;
						foreach (var kvp in obj)
						{
							Children.Add(new JsonNodeViewModel(kvp.Key, kvp.Value, this));
						}
					}
				}
				break;
			}
			case JsonValueKind.Array:
			{
				if (TryHelper.TryGet(() => node.AsArray(), out JsonArray? test) && test is JsonArray arr)
				{
					NodeType = JsonNodeType.Array;
					for (int i = 0; i < arr.Count; i++)
					{
						Children.Add(new JsonNodeViewModel($"[{i}]", arr[i], this));
					}
				}
				else
				{
					if (node is JsonArray arr2)
					{
						NodeType = JsonNodeType.Array;
						for (int i = 0; i < arr2.Count; i++)
						{
							Children.Add(new JsonNodeViewModel($"[{i}]", arr2[i], this));
						}
					}
				}
				break;
			}
			default:
				break;
		}

		Validate();
	}

	public void AddChildProperty()
	{
		if (NodeType == JsonNodeType.Object)
		{
			string newKey = $"property_{Children.Count + 1}";
			Children.Add(new JsonNodeViewModel(newKey, JsonValue.Create(""), this));
		}
		else if (NodeType == JsonNodeType.Array)
		{
			string newKey = $"[{Children.Count}]";
			Children.Add(new JsonNodeViewModel(newKey, JsonValue.Create(""), this));
		}
	}

	public void DeleteSelf()
	{
		Parent?.Children.Remove(this);
		Parent?.ReindexArrayChildren();
	}

	private void ReindexArrayChildren()
	{
		if (NodeType == JsonNodeType.Array)
		{
			for (int i = 0; i < Children.Count; i++)
			{
				Children[i].Key = $"[{i}]";
			}
		}
	}

	public void Validate()
	{
		HasError = false;
		ErrorMessage = string.Empty;

		if (NodeType == JsonNodeType.Number)
		{
			if (!double.TryParse(RawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
			{
				HasError = true;
				ErrorMessage = "Invalid number format.";
			}
		}
	}

	public JsonNode? ToJsonNode()
	{
		return NodeType switch
		{
			JsonNodeType.Object => BuildJsonObject(),
			JsonNodeType.Array => BuildJsonArray(),
			JsonNodeType.Number => double.TryParse(RawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var num) ? JsonValue.Create(num) : JsonValue.Create(0),
			JsonNodeType.Boolean => bool.TryParse(RawValue, out var b) ? JsonValue.Create(b) : JsonValue.Create(false),
			JsonNodeType.Null => null,
			_ => JsonValue.Create(RawValue)
		};
	}

	private JsonObject BuildJsonObject()
	{
		var obj = new JsonObject();
		foreach (var child in Children)
		{
			obj[child.Key] = child.ToJsonNode();
		}
		return obj;
	}

	private JsonArray BuildJsonArray()
	{
		var arr = new JsonArray();
		foreach (var child in Children)
		{
			arr.Add(child.ToJsonNode());
		}
		return arr;
	}

	private void NotifyTypeFlags()
	{
		OnPropertyChanged(nameof(IsContainer));
		OnPropertyChanged(nameof(IsObject));
		OnPropertyChanged(nameof(IsArray));
		OnPropertyChanged(nameof(IsString));
		OnPropertyChanged(nameof(IsNumber));
		OnPropertyChanged(nameof(IsBoolean));
		OnPropertyChanged(nameof(IsNull));
		OnPropertyChanged(nameof(TypeGlyph));
		OnPropertyChanged(nameof(TypeColorHex));
	}
	public string TypeColorHex => NodeType switch
	{
		JsonNodeType.Object => "#0078D4",  // Fluent Accent Blue
		JsonNodeType.Array => "#881798",   // Purple
		JsonNodeType.String => "#0F7B0F",  // Forest Green
		JsonNodeType.Number => "#D13438",  // Crimson / Red
		JsonNodeType.Boolean => "#008272", // Teal
		_ => "#797775"                     // Neutral Gray
	};
	public event PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
public enum JsonNodeType
{
	Object,
	Array,
	String,
	Number,
	Boolean,
	Null
}