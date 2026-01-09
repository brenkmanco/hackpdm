using System.Diagnostics.CodeAnalysis;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.OdooModels.Models;

public interface IHpOdooRecord
{
    [OdooProp(OdooFieldType.Integer)] public int id { get; set; }
}


public interface IHpWindowRootFunctions
{
    [OdooProp(OdooFieldType.Char)] public string? windows_complete_name { get; set; }
}