namespace HackPDM.Core.Hack;

/// <summary>A directory picked in the file manager's tree.</summary>
/// <param name="Name">Folder name.</param>
/// <param name="DirectoryId">Odoo directory id; 0 for a folder that only exists locally.</param>
/// <param name="TreePath">Path in the tree, starting at the root node ("root\a\b").</param>
/// <param name="IsRemoteOnly">True when the folder exists in Odoo but not on disk.</param>
public sealed record DirectoryRef( string Name, int DirectoryId, string TreePath, bool IsRemoteOnly );

/// <summary>What the preview pane should show: a server preview image, or a local file's shell icon.</summary>
public sealed record EntryPreview( byte[]? ImageBytes, string? LocalFilePath );
