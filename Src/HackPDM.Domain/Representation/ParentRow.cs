using HackPDM.Abstractions;

namespace HackPDM.Domain.Representation;

public class ParentRow : DataGridData
{
	// (MVVM) VIEW
	public int          Version     { get; set; }
	public string?      BasePath    { get; set; }
	public ParentRow() {}
}


