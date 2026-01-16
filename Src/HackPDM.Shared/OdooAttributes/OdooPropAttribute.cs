using System;
using System.Diagnostics.CodeAnalysis;

using HackPDM.Shared.GlobalData;

namespace HackPDM.Shared.OdooAttributes;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
[SuppressMessage("Resharper", "InconsistentNaming")]
public class OdooPropAttribute(OdooFieldType odooType, string odooFieldName) : Attribute
{
    public OdooFieldType OdooType { get; } = odooType;
    public string OdooFieldName { get; } = odooFieldName;
}