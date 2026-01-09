using HackPDM.Abstractions;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.Representation;

//public class SearchPropRow : DataGridData, IRowData
//{
//	// (MVVM) VIEW
//	public string? Comparer { get; set; }
//	public string? Value { get; set; }
//}
public class SearchPropertiesRow : DataGridData, IRowData<SearchPropertiesRow>
{
	// (MVVM) VIEW
	public int ID { get; set; }
	public Operators Comparer { get; set; }
	public string? Value { get; set; }
	public bool IsTextOrDate { get; set; }
	public SearchPropertiesRow Clone()
	{
		var cItem = new SearchPropertiesRow
		{
			ID = this.ID,
			Name = this.Name is null ?  null : new(this.Name),
			Text = this.Text is null ?  null : new(this.Text),
			Comparer = this.Comparer,
			Value = this.Value is null ?  null : new(this.Value),
			IsTextOrDate = this.IsTextOrDate,
		};

		return cItem;
	}
}


