using System;
using HackPDM.Abstractions;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.Representation;

public class PropertiesRow : DataGridData
{
	// (MVVM) VIEW
	public int          Version     { get; set; }
	public int?         Property    { get; set; }
	public string?      Configuration{  get; set; }
	public PropertyType? Type        { get; set; }
	public object?      ValueData       { get; set; }
	public PropertiesRow() {}
}


