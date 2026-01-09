using System;
using System.Collections.Generic;
using System.Text;

namespace HackPDM.Domain.OdooModels.Models;

public class HpEntryMiniModel
{
	public string? Name;
	public string? Type;
	public long? Size;
	public string? Checkout;
	public string? Fullname;
	public DateTime? LatestDate;
	public bool? Deleted;
	public string? LatestChecksum;
	public string? Category;
}