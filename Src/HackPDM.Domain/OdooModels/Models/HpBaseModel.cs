using System;
using System.Collections;
using System.Collections.Generic;
//
using System.IO;
using System.Linq;
using System.Reflection;

using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;
//


namespace HackPDM.Domain.OdooModels.Models;

public abstract partial class HpBaseModel
{
	// (MVVM) VIEW
	public const int ROOT_OFFSET = 5;
	[OdooProp(OdooFieldType.Integer, "id")] public int? id { get; set; }
	[OdooProp(OdooFieldType.Many2One, "commit_id")] public virtual IMany2One? commit_id { get; set; }
	

	public static string[] UsualExcludedFields { get; set; } = [];


    public virtual void CompleteConstruction() { }
    // ID of the record in the database
    /// <summary>The Odoo model name, from the [OdooModel] attribute on this class or one of its interfaces.</summary>
    public virtual string? HpModel => ModelNameOf(GetType());

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, string?> ModelNames = new();

    /// <summary>The Odoo model name declared by [OdooModel] on a model class or interface (or one it implements).</summary>
    public static string? ModelNameOf(Type type)
        => ModelNames.GetOrAdd(type, static t =>
            t.GetCustomAttribute<OdooModelAttribute>()?.DBName
            ?? t.GetInterfaces().Select(i => i.GetCustomAttribute<OdooModelAttribute>()?.DBName).FirstOrDefault(n => n is not null));
    
    public bool IsRecord { get; set; }
    /// <summary>Fields read from the server that have no property (and values set for staging).</summary>
    public Dictionary<string, object?> HashedValues { get; set; } = [];
    public string[]? ExcludedFields { get; set; }
    public string[]? InsertFields { get; set; }
}
