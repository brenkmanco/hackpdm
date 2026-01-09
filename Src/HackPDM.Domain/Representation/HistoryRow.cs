using HackPDM.Abstractions;
using HackPDM.Domain.OdooModels.Models;

namespace HackPDM.Domain.Representation;

public class HistoryRow : DataGridData, IRowData<HistoryRow>
{
	// (MVVM) VIEW
	public int          Version     { get; set; }
	public IHpUserModel?      ModUser     { get; set; }
	public DateTime?    ModDate     { get; set; }
	public long?         Size        { get; set; }
	public DateTime?    RelDate     { get; set; }
	public HistoryRow() {}
	public HistoryRow Clone()
	{
		var cItem = new HistoryRow
		{
			Name = this.Name is null ? null : new(Name),
			Text = this.Text is null ? null : new(Text),
			Version = this.Version,
			ModUser = this.ModUser,
			ModDate = this.ModDate,
			Size = this.Size,
			RelDate = this.RelDate,
		};
		
		return cItem;
	}
}


