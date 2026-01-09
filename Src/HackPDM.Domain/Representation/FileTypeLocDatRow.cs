using HackPDM.Abstractions;

namespace HackPDM.Domain.Representation;

public class FileTypeLocDatRow : DataGridData, IRowData<FileTypeLocDatRow>
{
	// (MVVM) VIEW
	public string? Extension { get; set; }
	public string? RegEx { get; set; }
	public string? Category { get; set; }
	public string? Description { get; set; }
	public object? Icon { get; set; } // Type is Unknown, so use object
	public object? RemoveIcon { get; set; } // Type is Unknown, so use object
	public FileTypeLocDatRow Clone()
	{
		var cItem = new FileTypeLocDatRow
		{
			Name = this.Name is null ? null : new(Name),
			Text = this.Text is null ? null : new(Text),
			Extension = this.Extension is null ? null : new(Extension),
			RegEx = this.RegEx is null ? null : new(RegEx),
			Category = this.Category is null ? null : new(Category),
			Description = this.Description is null ? null : new(Description),
			Icon = this.Icon,
			RemoveIcon = this.RemoveIcon,
		};

		return cItem;
	}
}


