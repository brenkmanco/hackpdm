
using HackPDM.Abstractions;

namespace HackPDM.Domain.Representation;

public class ChildrenRow : DataGridData, IRowData<ChildrenRow>
{
	// (MVVM) VIEW
	public int Version { get; set; }
	public string? BasePath { get; set; }
	public ChildrenRow() { }
	public ChildrenRow Clone()
	{
		var cItem = new ChildrenRow
		{
			Name = new (this.Name ?? ""),
			Text = new (this.Text ?? ""),
			Version = this.Version,
			BasePath = new (this.BasePath ?? ""),
		};

		return cItem;
	}
}


