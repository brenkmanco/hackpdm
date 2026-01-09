using HackPDM.Abstractions;

namespace HackPDM.Domain.Representation;

public class FileTypeRow : DataGridData, IRowData<FileTypeRow>
{
	// (MVVM) VIEW
	public string? Extension { get; set; }
	public string? Category { get; set; }
	public string? RegEx { get; set; }
	public string? Description { get; set; }
	public FileTypeRow Clone()
	{
		var cItem = new FileTypeRow
		{
			Name = this.Name is null ? null : new(Name),
			Text = this.Text is null ? null : new(Text),
			Extension = this.Extension is null ? null : new(Extension),
			Category = this.Category is null ? null : new(Category),
			RegEx = this.RegEx is null ? null : new(RegEx),
			Description = this.Description is null ? null : new(Description),
		};

		return cItem;
	}
}


