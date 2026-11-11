using HackPDM.Abstractions;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.Representation;

//public class SearchPropRow : DataGridData, IRowData
//{
//	// (MVVM) VIEW
//	public string? Comparer { get; set; }
//	public string? Value { get; set; }
//}
public class SearchPropertiesRow : DataGridData
{
	// (MVVM) VIEW
	public int ID { get; set; }
	public Operators Comparer { get; set; }
	public string? Value { get; set; }
	public bool IsTextOrDate { get; set; }
}


