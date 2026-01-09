using HackPDM.Abstractions;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.Representation;

public partial class OperatorsRow : DataGridData, IRowData<OperatorsRow>
{
	// (MVVM) VIEW
	public partial Operators Operator { get; set; }
	public partial string? OpRepr { get; set; }

	public OperatorsRow Clone()
	{
		var cItem = new OperatorsRow
		{
			Name = this.Name is null ?  null : new(this.Name),
			Text = this.Text is null ?  null : new(this.Text),
			Operator = this.Operator,
			OpRepr = this.OpRepr is null ?  null : new(this.OpRepr)
		};
		return cItem;
	}
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

