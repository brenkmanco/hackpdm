using System.Collections;
using System.Collections.ObjectModel;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.Representation;

/// <summary>A node of the directory tree as the UI shows it.</summary>
public abstract partial class TreeData
{
	// (MVVM) VIEW
	public virtual string? Name { get; set; }
	public virtual string? FullPath { get; }
	public virtual object? Tag { get; set; }
	/// <summary>The hp.directory id of the folder; <see langword="null"/> or 0 for a folder that only exists locally.</summary>
	public virtual int? DirectoryId { get; set; }

	public abstract bool IsExpanded { get; set; }
	/// <summary>How many levels below the tree's root the node is (the UI implementation reports -1 when not linked).</summary>
	public abstract int Depth { get; }
	public abstract bool HasChildren { get; }
	/// <summary>Whether the data is attached to a node of the UI tree control.</summary>
	public abstract bool IsLinked { get; }
	/// <summary>Sorts this node's children, and theirs, in display order.</summary>
	public abstract void SortTree();
}
