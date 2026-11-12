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
	public void Keep( string? codeText = null )
		=> _draft.Text = codeText ?? ( RootNodes.Count > 0 ? ToJson() : null );

	/// <summary>Loads the saved settings file (or a sample when there is none).</summary>
	[RelayCommand]
	private void Load() => Show( AppConfigJsonService.Load() );

	/// <summary>The tree as indented JSON.</summary>
	public string ToJson()
	{
		JsonObject root = [];
		foreach( JsonNodeViewModel node in RootNodes )
			root[ node.Key ] = node.ToJsonNode();
		return AppConfigJsonService.Format( root );
	}

	/// <summary>Replaces the tree with the JSON text; the parse error when it isn't valid (the tree is then unchanged).</summary>
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

	public void Save( string json ) => AppConfigJsonService.Save( json );

	/// <summary>Removes a node, whether it is top level or nested.</summary>
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
