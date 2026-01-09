using System.Diagnostics.CodeAnalysis;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.OdooModels;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
[SuppressMessage("Resharper", "InconsistentNaming")]
public class OdooPropAttribute(OdooFieldType odooType) : Attribute
{
    public OdooFieldType OdooType { get; } = odooType;
}