using System;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Shared.OdooAttributes;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
public class OdooFieldAttribute(OdooFieldType odooType) : Attribute
{
    public OdooFieldType OdooType { get; } = odooType;
}