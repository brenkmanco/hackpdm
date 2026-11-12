using System;
using System.Collections;

using HackPDM.Shared.GlobalData;

namespace HackPDM.Shared.OdooAttributes;

// odoo model db name
// odoo model name
/// <summary>Marks a class or interface as the client-side form of an Odoo model; the source generator and readers use it to find the model name.</summary>
/// <param name="odooName">The model's table-style name with underscores, such as <c>hp_entry</c>.</param>
/// <param name="odooDBName">The model's technical name, such as <c>hp.entry</c>, used in RPC calls.</param>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = true)]
public class OdooModelAttribute(string odooName, string odooDBName) : Attribute
{
    public string OdooName { get; } = odooName;
    public string DBName { get; } = odooDBName;
}