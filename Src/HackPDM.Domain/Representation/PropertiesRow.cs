using HackPDM.Abstractions;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.Representation;

public class PropertiesRow : DataGridData, IRowData<PropertiesRow>
{
	// (MVVM) VIEW
	public int          Version     { get; set; }
	public int?         Property    { get; set; }
	public string?      Configuration{  get; set; }
	public PropertyType? Type        { get; set; }
	public object?      ValueData       { get; set; }
	public PropertiesRow() {}
	public PropertiesRow Clone()
	{
		var cItem = new PropertiesRow
		{
			Name = this.Name is null ?  null : new(this.Name),
			Text = this.Text is null ?  null : new(this.Text),
			Version = this.Version,
			Property = this.Property,
			Configuration = this.Configuration is null ?  null : new(this.Configuration),
			Type = this.Type,
			ValueData = Activator.CreateInstance(this.ValueData!.GetType()),
		};
		
		return cItem;
	}
}


