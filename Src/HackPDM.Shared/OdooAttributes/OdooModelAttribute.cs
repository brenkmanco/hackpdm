using System;
using System.Collections;

using HackPDM.Shared.GlobalData;

namespace HackPDM.Shared.OdooAttributes;

// odoo model db name
// odoo model name
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = true)]
public class OdooModelAttribute(string odooName, string odooDBName) : Attribute
{
    public string OdooName { get; } = odooName;
    public string DBName { get; } = odooDBName;
}