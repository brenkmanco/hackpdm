using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using HackPDM.Mvvm.ViewModels.Tools;

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Windows.UI;

namespace HackPDM.Mvvm.Views.Tools;

/// <summary>Edits AppConfig.json as a tree or as text; the tree lives in <see cref="JsonEditorViewModel"/>.</summary>
public sealed partial class JsonEditorView : Page
{
	public JsonEditorViewModel ViewModel { get; } = new( HackPDM.Shell.AppComposition.JsonDraft );

	public JsonEditorView()
	{
		InitializeComponent();
	}

	/// <summary>The page is left: what the editor shows is kept for the next visit.</summary>
	protected override void OnNavigatedFrom( Microsoft.UI.Xaml.Navigation.NavigationEventArgs e )
	{
		base.OnNavigatedFrom( e );
		ViewModel.Keep( _isCodeView ? GetCodeText() : null );
	}

	private async void OnExportJsonClick( object sender, RoutedEventArgs e )
	{
		string exportedJson = ViewModel.ToJson();

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
			ViewModel.Save( exportedJson );
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
			ViewModel.Remove(vm);
	}

	// Recalculates TreeViewItemIndent as items expand/collapse to keep deep trees on-screen
	private void UpdateDynamicIndent()
	{
		int maxExpandedDepth = GetMaxExpandedDepth(ViewModel.RootNodes, 0);

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
		// text kept from the last visit that didn't parse has no tree: show it as it was typed
		ApplyJsonSyntaxHighlighting(ViewModel.RootNodes.Count == 0 && ViewModel.KeptText is { } kept ? kept : ViewModel.ToJson());
	}

	private bool SyncCodeViewToTree()
	{
		if (ViewModel.LoadText(GetCodeText()) is not { } error)
			return true;
		JsonParseInfoBar.Message = $"JSON Syntax Error: {error}";
		JsonParseInfoBar.IsOpen = true;
		return false;
	}
#if WINDOWS
	private string GetCodeText()
	{
		CodeEditor.Document.GetText(TextGetOptions.None, out string codeText);
		return codeText;
	}

	private void ApplyJsonSyntaxHighlighting(string jsonText)
	{
		// Fix index drift: Normalize newlines to match RichEditBox's internal 1-character format (\r)
		string normalizedJson = jsonText.Replace("\r\n", "\r");

		var doc = CodeEditor.Document;
		doc.SetText(TextSetOptions.None, normalizedJson);

		// IDE color tokens for the page's light or dark theme (the light set stays readable on white)
		bool dark = ActualTheme != ElementTheme.Light;
		Color colorKey = dark ? Color.FromArgb(255, 156, 220, 254) : Color.FromArgb(255, 4, 81, 165);       // #9CDCFE / #0451A5
		Color colorString = dark ? Color.FromArgb(255, 206, 145, 120) : Color.FromArgb(255, 163, 21, 21);   // #CE9178 / #A31515
		Color colorNumber = dark ? Color.FromArgb(255, 181, 206, 168) : Color.FromArgb(255, 9, 134, 88);    // #B5CEA8 / #098658
		Color colorKeyword = dark ? Color.FromArgb(255, 86, 156, 214) : Color.FromArgb(255, 0, 0, 255);     // #569CD6 / #0000FF
		Color colorPunct = dark ? Color.FromArgb(255, 212, 212, 212) : Color.FromArgb(255, 59, 59, 59);     // #D4D4D4 / #3B3B3B

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
#else
	// Uno does not implement RichEditBox: the code view is a plain TextBox without colours
	private string GetCodeText() => CodeText.Text;

	private void ApplyJsonSyntaxHighlighting(string jsonText) => CodeText.Text = jsonText;
#endif
}
