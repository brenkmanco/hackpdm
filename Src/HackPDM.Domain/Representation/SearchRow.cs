using HackPDM.Abstractions;

namespace HackPDM.Domain.Representation;

public class SearchRow : DataGridData
{
	// (MVVM) VIEW
	public int? Id { get; set; }
	public string? Directory { get; set; }
}


