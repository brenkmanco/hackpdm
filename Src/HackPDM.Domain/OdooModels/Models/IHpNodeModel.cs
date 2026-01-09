using System.Collections;
using HackPDM.Shared.GlobalData;
// Resharper disable InconsistentNaming

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_NODE_NAME, OdooDefaultsConstants.HP_NODE)]
public interface IHpNodeModel : IHpOdooRecord
{
    [OdooProp(OdooFieldType.Char)] public string name { get; set; }
}
