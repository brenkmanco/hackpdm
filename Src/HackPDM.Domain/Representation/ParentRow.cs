using HackPDM.Abstractions;

namespace HackPDM.Domain.Representation;

public class ParentRow : DataGridData, IRowData<ParentRow>
{
	// (MVVM) VIEW
	public int          Version     { get; set; }
	public string?      BasePath    { get; set; }
	public ParentRow() {}
	public ParentRow Clone()
	{
		var cItem = new ParentRow
		{
			Name = this.Name is null ?  null : new(this.Name),
			Text = this.Text is null ?  null : new(this.Text),
			Version = this.Version,
			BasePath = this.BasePath is null ?  null : new(this.BasePath),
		};

		return cItem;
	}
}


