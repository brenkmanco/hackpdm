using System;
using HackPDM.Abstractions;
using HackPDM.Domain.OdooModels.Models;

namespace HackPDM.Domain.Representation;

public class HistoryRow : DataGridData
{
	// (MVVM) VIEW
	public int          Version     { get; set; }
	public IHpUserModel?      ModUser     { get; set; }
	public DateTime?    ModDate     { get; set; }
	public long?         Size        { get; set; }
	public DateTime?    RelDate     { get; set; }
	public HistoryRow() {}
}


