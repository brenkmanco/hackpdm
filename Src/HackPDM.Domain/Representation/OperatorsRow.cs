using HackPDM.Abstractions;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.Representation;

public partial class OperatorsRow : DataGridData
{
	// (MVVM) VIEW
	public partial Operators Operator { get; set; }
	public partial string? OpRepr { get; set; }

}
public partial class OperatorsRow
{
	// (MVVM) ViewModel
	public partial Operators Operator
	{
		get => field;
		set
		{
			field = value;
			OpRepr = OperatorConverter.OperatorToString(value);
		}
	}
	public partial string? OpRepr
	{
		get => field;
		set
		{
			field = value;
		}
	}
}

