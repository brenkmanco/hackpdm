using System.Collections;

using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.OdooModels;

// odoo model db name
// odoo model name
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = true)]
public class OdooModelAttribute(string odooName, string odooDBName) : Attribute
{
    public string OdooName { get; } = odooName;
    public string DBName { get; } = odooDBName;
}

public static class OdooFieldExtension
{
    public static readonly Dictionary<OdooFieldType, Type[]> Schema = new()
    {
        { OdooFieldType.Char,       new[] { typeof(string) } },
        { OdooFieldType.Text,       new[] { typeof(string) } },
        { OdooFieldType.Html,       new[] { typeof(string) } },
        { OdooFieldType.Integer,    new[] { typeof(int), typeof(long) } },
        { OdooFieldType.Float,      new[] { typeof(double), typeof(decimal) } },
        { OdooFieldType.Monetary,   new[] { typeof(decimal) } },
        { OdooFieldType.Boolean,    new[] { typeof(bool) } },
        { OdooFieldType.Date,       new[] { typeof(DateTime) } },
        { OdooFieldType.DateTime,   new[] { typeof(DateTime) } },
        { OdooFieldType.Binary,     new[] { typeof(string), typeof(byte[]) } }, // Odoo sometimes base64 encodes
        { OdooFieldType.Many2one,   new[] { typeof(object[]), typeof(ValueTuple<int,string>) } },
        { OdooFieldType.One2many,   new[] { typeof(int[]), typeof(ArrayList) } },
        { OdooFieldType.Many2many,  new[] { typeof(int[]), typeof(ArrayList) } },
        { OdooFieldType.Selection,  new[] { typeof(string), typeof(int) } },
        { OdooFieldType.Reference,  new[] { typeof(string), typeof(object[]) } },
        { OdooFieldType.Serialized, new[] { typeof(Dictionary<string,object>), typeof(string) } },
    };
}
