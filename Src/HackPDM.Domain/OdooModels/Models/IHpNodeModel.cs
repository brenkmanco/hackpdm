using System.Collections;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;
// Resharper disable InconsistentNaming

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_NODE_NAME, OdooDefaultsConstants.HP_NODE)]
public interface IHpNodeModel : IHpOdooRecord
{
    [OdooProp(OdooFieldType.Char, "name")] public string? name { get; set; }
}
