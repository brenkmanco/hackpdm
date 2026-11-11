using System.Text.RegularExpressions;

using HackPDM.Abstractions;

namespace HackPDM.Domain.Representation;

public class FileTypeLocRow : DataGridData
{
	// (MVVM) VIEW
	public string? Extension { get; set; }
	public string? Status { get; set; }
	public string? Example { get; set; }
}


