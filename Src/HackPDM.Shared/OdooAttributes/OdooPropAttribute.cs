using System;
using System.Diagnostics.CodeAnalysis;

using HackPDM.Shared.GlobalData;

namespace HackPDM.Shared.OdooAttributes;

/// <summary>Maps a property to an Odoo field; the source generator emits the read/write conversions from it.</summary>
/// <param name="odooType">The field's Odoo type, which decides how the value is converted to and from JSON-RPC.</param>
/// <param name="odooFieldName">The field's technical name on the server.</param>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
[SuppressMessage("Resharper", "InconsistentNaming")]
public class OdooPropAttribute(OdooFieldType odooType, string odooFieldName) : Attribute
{
    public OdooFieldType OdooType { get; } = odooType;
    public string OdooFieldName { get; } = odooFieldName;
}