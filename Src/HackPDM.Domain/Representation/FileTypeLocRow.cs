using System.Text.RegularExpressions;

using HackPDM.Abstractions;

namespace HackPDM.Domain.Representation;

public class FileTypeLocRow : DataGridData, IRowData<FileTypeLocRow>
{
	// (MVVM) VIEW
	public string? Extension { get; set; }
	public string? Status { get; set; }
	public string? Example { get; set; }
	public FileTypeLocRow Clone()
	{
		var cItem = new FileTypeLocRow
		{
			Name = this.Name is null ? null : new(Name),
			Text = this.Text is null ? null : new(Text),
			Extension = this.Extension is null ? null : new(Extension),
			Status = this.Status is null ? null : new(Status),
			Example = this.Example is null ? null : new(Example),
		};

		return cItem;
	}
}


