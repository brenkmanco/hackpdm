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


    /// <summary>
    /// Called after a record has been filled from a server row, to derive values from the raw fields
    /// (for example from <see cref="HashedValues"/>). Does nothing by default.
    /// </summary>
    public virtual void CompleteConstruction() { }
    // ID of the record in the database
    /// <summary>The Odoo model name, from the [OdooModel] attribute on this class or one of its interfaces.</summary>
    public virtual string? HpModel => ModelNameOf(GetType());

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, string?> ModelNames = new();

    /// <summary>The Odoo model name declared by [OdooModel] on a model class or interface (or one it implements).</summary>
    /// <param name="type">The model class or interface.</param>
    /// <returns>The technical model name, such as <c>hp.entry</c>, or <see langword="null"/> when none is declared.</returns>
    /// <remarks>The class's own attribute wins over its interfaces'; results are cached per type.</remarks>
    public static string? ModelNameOf(Type type)
        => ModelNames.GetOrAdd(type, static t =>
            t.GetCustomAttribute<OdooModelAttribute>()?.DBName
            ?? t.GetInterfaces().Select(i => i.GetCustomAttribute<OdooModelAttribute>()?.DBName).FirstOrDefault(n => n is not null));
    
    /// <summary>Whether this object stands for a record that exists on the server (read from it, or created successfully).</summary>
    public bool IsRecord { get; set; }
    /// <summary>Fields read from the server that have no property (and values set for staging).</summary>
    public Dictionary<string, object?> HashedValues { get; set; } = [];
    public string[]? ExcludedFields { get; set; }
    public string[]? InsertFields { get; set; }
}
