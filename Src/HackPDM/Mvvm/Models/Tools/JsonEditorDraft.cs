namespace HackPDM.Mvvm.Models.Tools;

/// <summary>
/// What the JSON editor showed when its page was left (saved or not), kept for the whole run (one
/// instance, held by the composition root) so coming back shows it again instead of an empty editor.
/// Null when the editor was empty.
/// </summary>
public sealed class JsonEditorDraft
{
	public string? Text { get; set; }
}
