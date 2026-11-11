using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Domain.OdooModels.Models;



//
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HackPDM.Core;
using HackPDM.Domain.OdooModels;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;
using SolidWorks.Interop.swdocumentmgr;
using EntryRow = HackPDM.Domain.Representation.EntryRow;
using TreeData = HackPDM.Domain.Representation.TreeData;
using OClient = HackPDM.Infrastructure.Odoo.OdooClient;
using Attribute = System.Attribute;
using HackPDM.Shared.OdooAttributes;

//

namespace HackPDM.Infrastructure.Odoo;

public static class Help
{
    // [0, 1, 2, 3, 4]
	// [0, 1], [2, 3], [4]
        
    // give the ArrayList class an extension method that selects
    public static IEnumerable<string> FastSlice(IEnumerable<string> source, int startIndex, string prependText = null, string appendText = null)
    {
        foreach (string str in source)
        {
            StringBuilder sb = new();

            // add prepended text
            if (prependText != null) sb.Append(prependText);
            // slice
            sb.Append(str.AsSpan()[startIndex..].ToString());
            // add appended text
            if (appendText != null) sb.Append(appendText);

            yield return sb.ToString();
        }
    }
    public static SwDmDocumentType GetSwDmDocumentTypeFromExtension(string file_ext)
        => file_ext.ToLower() switch
        {
            "sldprt" => SwDmDocumentType.swDmDocumentPart,
            "sldasm" => SwDmDocumentType.swDmDocumentAssembly,
            "slddrw" => SwDmDocumentType.swDmDocumentDrawing,
            _ => SwDmDocumentType.swDmDocumentUnknown,
        };
}
public static class HashConverter
{
	static readonly Type boolType = typeof(bool);
	static readonly Type arrType = typeof(ArrayList);

    public static T? ConvertToClass<T>(in Hashtable ht) where T : HpBaseModelTransport, new()
    {
		T record = new();
        return AssignToClass(ht, ref record);
	}
	public static T[]? ConvertToClasses<T>(in IEnumerable<Hashtable>? hts) where T : HpBaseModelTransport, new()
	{
		(Hashtable, T)[] records = [.. hts?.PopulateZip(obj => new T()) ?? []];
        var values = records.AsSpan();
        return AssignToClasses(ref values).SelectSecond();
	}
	
    public static ref T AssignToClass<T>(in Hashtable ht, ref T record) where T : HpBaseModelTransport
    {
		var map = OdooAssignments<T>.Map;
		
		if (ht.TryGetValue("id", out int? id) && id is not null)
			record.id = id ?? 0;

		foreach (DictionaryEntry entry in ht)
		{
			if (map.TryGetValue(entry.Key as string ?? "", out var assign))
				assign(record, entry.Value);
			else
				record.HashedValues.Add( entry.Key, entry.Value );
		}
		return ref record;
	}
    public static void AssignToClass<T>(in Hashtable ht, T record) where T : HpBaseModelTransport
    {
		var map = OdooAssignments<T>.Map;

		if (ht.TryGetValue("id", out int? id) && id is not null)
			record.id = id ?? 0;

		foreach (DictionaryEntry entry in ht)
		{
			if (map.TryGetValue(entry.Key as string ?? "", out var assign))
				assign(record, entry.Value);
			else
				record.HashedValues.Add( entry.Key, entry.Value );
		}
	}
	public static ref Span<(Hashtable, T)> AssignToClasses<T>(ref Span<(Hashtable, T)> values) where T : HpBaseModelTransport
    {
        if (values.Length == 0) return ref values;
		var map = OdooAssignments<T>.Map;
        
		for (int i = 0; i < values.Length; i++)
        {
            ref (Hashtable ht, T record) record = ref values[i];

			if (record.ht.TryGetValue("id", out int? id) && id is not null)
				record.record.id = id ?? 0;
            
		    foreach (DictionaryEntry entry in record.ht)
		    {
                if( map.TryGetValue( entry.Key as string ?? "", out var assign ) )
                    assign( record.record, entry.Value );
                else
                    record.record.HashedValues.Add( entry.Key, entry.Value );
		    }
		}
        return ref values;
	}
    // first case: value is nullable but target type isn't
    // second case: target type is nullable but value isn't
    // 
}