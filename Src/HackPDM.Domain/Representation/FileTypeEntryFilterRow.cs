using HackPDM.Abstractions;

namespace HackPDM.Domain.Representation;

public class FileTypeEntryFilterRow : DataGridData
{
	// (MVVM) VIEW
	public int Id { get; set; }
	public string? Proto { get; set; }
	public string? RegEx { get; set; }
	public string? Description { get; set; }
}


