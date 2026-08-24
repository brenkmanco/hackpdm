using System.Diagnostics.CodeAnalysis;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

namespace HackPDM.Domain.OdooModels.Models;

public interface IHpOdooRecord
{
    [OdooProp(OdooFieldType.Integer, "id")] public int? id { get; set; }
	[OdooProp(OdooFieldType.Many2One, "commit_id")] public IMany2One? commit_id { get; set; }
}

public interface IMany2One : IHpOdooRecord 
{ 
    public string? name { get; set; }
}
public interface IMultiRecord { public IHpOdooRecord?[]? Ids { get; set; } }
public interface IMany2Many : IMultiRecord { }
public interface IOne2Many : IMultiRecord { }
public interface IHpWindowRootFunctions
{
    [OdooProp(OdooFieldType.Char, "windows_complete_name")] public string? windows_complete_name { get; set; }
}