using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

using HackPDM.Infrastructure.Odoo;

namespace HackPDM.Mvvm.ViewModels.Tools;

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
