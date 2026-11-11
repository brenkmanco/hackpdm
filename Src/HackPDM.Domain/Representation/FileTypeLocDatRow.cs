using HackPDM.Abstractions;

namespace HackPDM.Domain.Representation;

public class FileTypeLocDatRow : DataGridData
{
	// (MVVM) VIEW
	public string? Extension { get; set; }
	public string? RegEx { get; set; }
	public string? Category { get; set; }
	public string? Description { get; set; }
	public object? Icon { get; set; } // Type is Unknown, so use object
	public object? RemoveIcon { get; set; } // Type is Unknown, so use object
}


