using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Mvvm.Models.Tools;

namespace HackPDM.Mvvm.ViewModels.Tools;

/// <summary>
/// The settings file as an editable tree of top-level properties. One per visit of the page: it starts
/// from what the editor showed when the page was last left (<see cref="JsonEditorDraft"/>).
/// </summary>
public partial class JsonEditorViewModel : ObservableObject
{
	private readonly JsonEditorDraft _draft;

	/// <summary>Creates the editor, showing the kept draft as a tree when it parses.</summary>
	/// <param name="draft">The run-wide store of what the editor last showed.</param>
	public JsonEditorViewModel( JsonEditorDraft draft )
	{
		_draft = draft;
		// text that no longer parses (left mid-edit in the code view) is still kept for that view
		if( draft.Text is { } text )
			LoadText( text );
	}

	public ObservableCollection<JsonNodeViewModel> RootNodes { get; } = [];

	/// <summary>The text the code view should start with: the kept text, which may not parse yet.</summary>
	public string? KeptText => _draft.Text;

	/// <summary>The page is left: keeps the tree (or, from the code view, its text as typed).</summary>
	/// <param name="codeText">The code view's text when the page is left from it; null keeps the tree as JSON (or nothing when it is empty).</param>
	public void Keep( string? codeText = null )
		=> _draft.Text = codeText ?? ( RootNodes.Count > 0 ? ToJson() : null );

	/// <summary>Loads the saved settings file (or a sample when there is none).</summary>
	[RelayCommand]
	private void Load() => Show( AppConfigJsonService.Load() );

	/// <summary>Converts the tree back to text.</summary>
	/// <returns>The top-level properties, in order, as indented JSON.</returns>
	public string ToJson()
	{
		JsonObject root = [];
		foreach( JsonNodeViewModel node in RootNodes )
			root[ node.Key ] = node.ToJsonNode();
		return AppConfigJsonService.Format( root );
	}

	/// <summary>Replaces the tree with the JSON text.</summary>
	/// <param name="json">The JSON document; a top level that is not an object gives an empty tree.</param>
	/// <returns>Null on success; the parse error message when the text isn't valid JSON (the tree is then unchanged).</returns>
	public string? LoadText( string json )
	{
		try
		{
			Show( JsonNode.Parse( json ) as JsonObject ?? [] );
			return null;
		}
		catch( JsonException ex )
		{
			return ex.Message;
		}
	}

	/// <summary>Writes the JSON as the app's settings file (AppConfig.json).</summary>
	/// <param name="json">The full settings document.</param>
	/// <remarks>Fields the app doesn't know are dropped. The running app does not reload the settings.</remarks>
	/// <exception cref="System.Text.Json.JsonException"><paramref name="json"/> is not valid settings JSON.</exception>
	/// <exception cref="System.IO.IOException">The settings file cannot be written.</exception>
	public void Save( string json ) => AppConfigJsonService.Save( json );

	/// <summary>Removes a node, whether it is top level or nested.</summary>
	/// <param name="node">The node to remove, with its children.</param>
	public void Remove( JsonNodeViewModel node )
	{
		if( node.Parent is not null )
			node.DeleteSelf();
		else
			RootNodes.Remove( node );
	}

	private void Show( JsonObject root )
	{
		RootNodes.Clear();
		foreach( var (key, value) in root )
			RootNodes.Add( new JsonNodeViewModel( key, value ) );
	}
}
