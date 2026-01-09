using HackPDM.Abstractions;

namespace HackPDM.Domain.Representation;

public class FileTypeEntryFilterRow : DataGridData, IRowData<FileTypeEntryFilterRow>
{
	// (MVVM) VIEW
	public int Id { get; set; }
	public string? Proto { get; set; }
	public string? RegEx { get; set; }
	public string? Description { get; set; }
	public FileTypeEntryFilterRow Clone()
	{
		var cItem = new FileTypeEntryFilterRow
		{
			Name = new(this.Name ?? ""),
			Text = new(this.Text ?? ""),
			Id = this.Id,
			Proto = new(this.Proto ?? ""),
			RegEx = new(this.RegEx ?? ""),
			Description = new(this.Description ?? ""),
		};

		return cItem;
	}
}


