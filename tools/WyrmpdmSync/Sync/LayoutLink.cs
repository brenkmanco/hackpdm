namespace WyrmpdmSync.Sync;

/// <summary>
/// A XAML view and its Penpot frames: the Light frame is compared with the code, the Dark frame (same layer
/// structure, Dark tokens) is written alongside it.
/// </summary>
internal sealed class FrameLink
{
	/// <summary>The view, repository-relative ("Src/HackPDM/Graph/DependencyGraphView.xaml").</summary>
	public required string View { get; set; }
	/// <summary>The Light frame's name in Penpot ("Dependency Graph — Light").</summary>
	public required string Frame { get; set; }
	/// <summary>The Dark frame's name, if the design has one.</summary>
	public string? DarkFrame { get; set; }
	public Guid? FrameId { get; set; }
	public Guid? DarkFrameId { get; set; }
	public List<ElementLink> Elements { get; set; } = [];
}

/// <summary>
/// One XAML element (marked <c>pp:Id="…"</c>) and its Penpot layer. <see cref="Path"/> names the layer from the frame
/// down ("Content/Toolbar", "Content/Workspace/Folders pane/Header"); "#2" picks the second of same-named siblings in
/// visual order. `init` turns the path into ids, so renaming layers in Penpot afterwards doesn't break the link.
/// </summary>
internal sealed class ElementLink
{
	public required string Anchor { get; set; }
	public string? Path { get; set; }
	public Guid? Shape { get; set; }
	public Guid? DarkShape { get; set; }
	/// <summary>Synced property → value both sides had at the last sync (null when they never agreed).</summary>
	public Dictionary<string, string?> Properties { get; set; } = [];
	/// <summary>
	/// When true, the element's children are kept in step as a whole: a layer added in Penpot inside it becomes a new
	/// XAML element, and a new marked XAML element inside it becomes a Penpot layer. Every child must be marked.
	/// </summary>
	public bool Children { get; set; }
}
