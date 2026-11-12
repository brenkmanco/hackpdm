using System.Collections;
using System.Text.Json;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>
/// The values Odoo sends and expects, as plain .NET values: a JSON object becomes a
/// <see cref="Dictionary{TKey,TValue}"/> of string to object, an array a <see cref="List{T}"/> of object,
/// numbers int / long / double. Odoo sends <c>false</c> for an empty non-boolean field.
/// </summary>
public static class OdooValues
{
	/// <summary>Converts a JSON-RPC result element into plain .NET values, recursively.</summary>
	/// <param name="element">The JSON element to convert.</param>
	/// <returns>
	/// A <see cref="Dictionary{TKey,TValue}"/> of string to object for an object, a <see cref="List{T}"/> of object for an array,
	/// a string, a boxed int / long / double for a number (the smallest that fits), a bool, or null for JSON null.
	/// Odoo's <c>false</c> for an empty field stays <see langword="false"/>.
	/// </returns>
	public static object? FromJson( JsonElement element )
		=> element.ValueKind switch
		{
			JsonValueKind.Object => element.EnumerateObject().ToDictionary( p => p.Name, p => FromJson( p.Value ) ),
			JsonValueKind.Array => element.EnumerateArray().Select( FromJson ).ToList(),
			JsonValueKind.String => element.GetString(),
			// boxed: unboxed, `? i : ? l : d` would type every number as double
			JsonValueKind.Number => element.TryGetInt32( out int i ) ? i : element.TryGetInt64( out long l ) ? (object)l : element.GetDouble(),
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			JsonValueKind.Null or JsonValueKind.Undefined => null,
			_ => element.GetRawText(),
		};

	/// <summary>Prepares a value to be sent over JSON-RPC.</summary>
	/// <param name="value">The value to convert.</param>
	/// <returns>
	/// <see cref="OdooDomain"/> and <see cref="DomainTerm"/> values as their RPC arrays, any other non-string, non-dictionary
	/// enumerable (such as an id array) as a <see cref="List{T}"/> with each item converted recursively, and everything else unchanged.
	/// </returns>
	public static object? ToRpc( object? value )
		=> value switch
		{
			OdooDomain domain => domain.ToRpc(),
			DomainTerm term => term.ToRpc(),
			string or null => value,
			IDictionary => value,
			IEnumerable items => items.Cast<object?>().Select( ToRpc ).ToList(),
			_ => value,
		};

	/// <summary>Extracts the id from a many2one value (<c>[id, "name"]</c>) or a plain integer id.</summary>
	/// <param name="value">A many2one pair, an int / long id, or Odoo's <see langword="false"/>.</param>
	/// <returns>The id, or null for an empty value (false), an empty list, or any other shape.</returns>
	public static int? Many2OneId( object? value )
		=> value switch
		{
			IList { Count: > 0 } list => AsInt( list[ 0 ] ),
			int or long => AsInt( value ),
			_ => null,
		};

	/// <summary>Extracts the display name from a many2one value (<c>[id, "name"]</c>).</summary>
	/// <param name="value">A many2one pair or Odoo's <see langword="false"/>.</param>
	/// <returns>The display name, or null when the value is empty (false) or not a pair.</returns>
	public static string? Many2OneName( object? value ) => value is IList { Count: > 1 } list ? list[ 1 ] as string : null;

	/// <summary>Extracts the ids of a one2many / many2many value, or of any list of ids.</summary>
	/// <param name="value">An enumerable of ids; items that are not int (or long in int range) are skipped.</param>
	/// <returns>The ids, or an empty list when <paramref name="value"/> is not a list (Odoo's false included).</returns>
	public static IReadOnlyList<int> Ids( object? value )
		=> value is IEnumerable items and not string
			? [ .. items.Cast<object?>().Select( AsInt ).OfType<int>() ]
			: [];

	/// <summary>Reads an integer from a plain value.</summary>
	/// <param name="value">The value; an int, or a long within int range, is accepted.</param>
	/// <returns>The integer, or null for anything else (Odoo's false, strings and floats included).</returns>
	public static int? AsInt( object? value )
		=> value switch
		{
			int i => i,
			long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
			_ => null,
		};

	/// <summary>Reads a string from a plain value.</summary>
	/// <param name="value">The value.</param>
	/// <returns>The string, or null when the value is not a string (Odoo's false included).</returns>
	public static string? AsString( object? value ) => value as string;

	/// <summary>Reads a row's field as a string.</summary>
	/// <param name="row">A row from <see cref="OdooRpc"/>, for example an <see cref="OdooRow"/>.</param>
	/// <param name="field">The field name.</param>
	/// <returns>The string, or null when the field is missing, false, or not a string.</returns>
	public static string? Text( this IReadOnlyDictionary<string, object?> row, string field ) => row.GetValueOrDefault( field ) as string;

	/// <summary>Reads a row's integer field.</summary>
	/// <param name="row">A row from <see cref="OdooRpc"/>.</param>
	/// <param name="field">The field name.</param>
	/// <returns>The integer, or null when the field is missing, false, or not an integer (see <see cref="AsInt"/>).</returns>
	public static int? Int( this IReadOnlyDictionary<string, object?> row, string field ) => AsInt( row.GetValueOrDefault( field ) );

	/// <summary>Reads a row's boolean field.</summary>
	/// <param name="row">A row from <see cref="OdooRpc"/>.</param>
	/// <param name="field">The field name.</param>
	/// <returns>True only when the field holds <see langword="true"/>; false when it is missing or holds anything else.</returns>
	public static bool Bool( this IReadOnlyDictionary<string, object?> row, string field ) => row.GetValueOrDefault( field ) is true;

	/// <summary>Reads the id of a row's many2one field.</summary>
	/// <param name="row">A row from <see cref="OdooRpc"/>.</param>
	/// <param name="field">The many2one field name.</param>
	/// <returns>The referenced id, or null when the field is missing or empty (see <see cref="Many2OneId"/>).</returns>
	public static int? RefId( this IReadOnlyDictionary<string, object?> row, string field ) => Many2OneId( row.GetValueOrDefault( field ) );

	/// <summary>Reads the display name of a row's many2one field.</summary>
	/// <param name="row">A row from <see cref="OdooRpc"/>.</param>
	/// <param name="field">The many2one field name.</param>
	/// <returns>The display name, or null when the field is missing or empty.</returns>
	public static string? RefName( this IReadOnlyDictionary<string, object?> row, string field ) => Many2OneName( row.GetValueOrDefault( field ) );

	/// <summary>Reads the ids of a row's one2many / many2many field.</summary>
	/// <param name="row">A row from <see cref="OdooRpc"/>.</param>
	/// <param name="field">The x2many field name.</param>
	/// <returns>The ids, or an empty list when the field is missing or empty.</returns>
	public static IReadOnlyList<int> RefIds( this IReadOnlyDictionary<string, object?> row, string field ) => Ids( row.GetValueOrDefault( field ) );

	/// <summary>Reads a row's nested JSON object, such as a map returned by an addon method.</summary>
	/// <param name="row">A row from <see cref="OdooRpc"/>.</param>
	/// <param name="field">The field name.</param>
	/// <returns>The nested dictionary, or null when the field is missing or not an object.</returns>
	public static IReadOnlyDictionary<string, object?>? Object( this IReadOnlyDictionary<string, object?> row, string field )
		=> row.GetValueOrDefault( field ) as IReadOnlyDictionary<string, object?>;
}
