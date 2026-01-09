using HackPDM.Abstractions;

namespace HackPDM.Domain.Representation;

public class SearchRow : DataGridData, IRowData<SearchRow>
{
	// (MVVM) VIEW
	public int? Id { get; set; }
	public string? Directory { get; set; }
	public SearchRow Clone()
	{
		var cItem = new SearchRow
		{
			Name = this.Name is null ?  null : new(this.Name),
			Text = this.Text is null ?  null : new(this.Text),
			Id = this.Id,
			Directory = this.Directory,
		};

		return cItem;
	}
}


