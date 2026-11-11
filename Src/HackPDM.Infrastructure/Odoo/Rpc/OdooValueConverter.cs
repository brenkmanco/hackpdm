using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using HackPDM.Shared.GlobalData;
using HackPDM.Infrastructure.Odoo.Models;

using Newtonsoft.Json;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>
/// Turns a value read from Odoo (see <see cref="OdooValues.FromJson"/>) into the CLR type of a model
/// property of the given field type. The generated <c>OdooAssignments&lt;T&gt;</c> call <see cref="Convert"/>.
/// </summary>
public static class OdooValueConverter
{
	public static object? Convert( OdooFieldType type, object? value )
	{
		// Odoo sends false for an empty non-boolean field
		if( value is null || value is false && type != OdooFieldType.Boolean )
			return null;

		return type switch
		{
			OdooFieldType.Integer => ToInt( value ),
			OdooFieldType.Char or OdooFieldType.Text or OdooFieldType.Html
				or OdooFieldType.Selection or OdooFieldType.Reference => value as string ?? value.ToString(),
			OdooFieldType.Float => (float)ToDouble( value ),
			OdooFieldType.Monetary => value is decimal m ? m : (decimal)ToDouble( value ),
			OdooFieldType.Boolean => value is true,
			OdooFieldType.DateTime => ToDateTime( value ),
			OdooFieldType.Date => ToDateTime( value ).Date,
			OdooFieldType.Many2One => ToMany2One( value ),
			OdooFieldType.One2Many => new One2Many( OdooValues.Ids( value ) ),
			OdooFieldType.Many2Many => new Many2Many( OdooValues.Ids( value ) ),
			OdooFieldType.Json => ToJsonObject( value ),
			OdooFieldType.Serialized => ToSerialized( value ),
			OdooFieldType.Binary or OdooFieldType.Image => ToBinary( value ),
			_ => value,
		};
	}

	/// <summary>int / long; anything else (text included) is 0, as before.</summary>
	private static int ToInt( object value ) => value switch { int i => i, long l => (int)l, _ => 0 };

	/// <summary>Any number: a whole-valued float arrives as an int from the JSON reader.</summary>
	private static double ToDouble( object value ) => value switch
	{
		double d => d,
		float f => f,
		decimal m => (double)m,
		int i => i,
		long l => l,
		_ => 0,
	};

	private static DateTime ToDateTime( object value ) => value switch
	{
		DateTime date => date,
		string text when DateTime.TryParse( text, out DateTime parsed ) => parsed,
		_ => default,
	};

	/// <summary>[id, "name"]; any other shape is an empty reference.</summary>
	private static Many2One ToMany2One( object value )
		=> value is IList { Count: >= 2 } pair
			? new Many2One { id = OdooValues.AsInt( pair[ 0 ] ) ?? 0, name = pair[ 1 ] as string ?? "" }
			: new Many2One { id = 0, name = "" };

	/// <summary>Base64 text (null when it isn't valid base64) or bytes.</summary>
	private static byte[]? ToBinary( object value )
	{
		if( value is byte[] bytes )
			return bytes;
		if( value is not string { Length: > 0 } text )
			return null;
		try { return System.Convert.FromBase64String( text ); }
		catch( FormatException ) { return null; }
	}

	/// <summary>A JSON field: already a dictionary from the reader, or JSON text.</summary>
	private static Dictionary<string, object?> ToJsonObject( object value )
		=> value as Dictionary<string, object?> ?? ToSerialized( value ).ToDictionary( pair => pair.Key, pair => (object?)pair.Value );

	private static Dictionary<string, object> ToSerialized( object value )
	{
		if( value is IDictionary parsed )
		{
			Dictionary<string, object> copy = [];
			foreach( DictionaryEntry entry in parsed )
				copy[ entry.Key.ToString() ?? "" ] = entry.Value!;
			return copy;
		}
		return value is string { Length: > 0 } text
			? JsonConvert.DeserializeObject<Dictionary<string, object>>( text ) ?? []
			: [];
	}
}
