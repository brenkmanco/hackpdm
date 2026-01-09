using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.OdooModels;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
public class OdooFieldAttribute(OdooFieldType odooType) : Attribute
{
    public OdooFieldType OdooType { get; } = odooType;
}