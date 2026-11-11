
using HackPDM.Abstractions;

namespace HackPDM.Domain.Representation;

public class ChildrenRow : DataGridData
{
	// (MVVM) VIEW
	public int Version { get; set; }
	public string? BasePath { get; set; }
	public ChildrenRow() { }
}


