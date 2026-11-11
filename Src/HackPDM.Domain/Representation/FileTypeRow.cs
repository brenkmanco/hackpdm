using HackPDM.Abstractions;

namespace HackPDM.Domain.Representation;

public class FileTypeRow : DataGridData
{
	// (MVVM) VIEW
	public string? Extension { get; set; }
	public string? Category { get; set; }
	public string? RegEx { get; set; }
	public string? Description { get; set; }
}


