using System.Collections;
using System.Collections.ObjectModel;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.Representation;

public abstract partial class TreeData
{
	// (MVVM) VIEW
	public string? Name { get; set; }
	public string? FullPath { get; }
	public object? Tag { get; set; }
	public int? DirectoryId { get; set; }

	public abstract bool IsExpanded { get; set; }
	public abstract int Depth { get; }
	public abstract bool HasChildren { get; }
	public abstract bool IsLinked { get; }
	public abstract void SortTree();
}
