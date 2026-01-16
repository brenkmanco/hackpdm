using System;
using System.Diagnostics.CodeAnalysis;

using HackPDM.Shared.GlobalData;

namespace HackPDM.Shared.OdooAttributes;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
[SuppressMessage("Resharper", "InconsistentNaming")]
public class OdooFieldTypeAttribute(OdooFieldType odooType) : Attribute
{
    public OdooFieldType OdooType { get; } = odooType;
}