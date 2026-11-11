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
	/// <summary>A JSON-RPC result value as plain .NET values.</summary>
	public static object? FromJson( JsonElement element )
		=> element.ValueKind switch
		{
			JsonValueKind.Object => element.EnumerateObject().ToDictionary( p => p.Name, p => FromJson( p.Value ) ),
			JsonValueKind.Array => element.EnumerateArray().Select( FromJson ).ToList(),
			JsonValueKind.String => element.GetString(),
			JsonValueKind.Number => element.TryGetInt32( out int i ) ? i : element.TryGetInt64( out long l ) ? l : element.GetDouble(),
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			JsonValueKind.Null or JsonValueKind.Undefined => null,
			_ => element.GetRawText(),
		};

	/// <summary>A value as it is sent: domains and id lists become arrays.</summary>
	public static object? ToRpc( object? value )
		=> value switch
		{
			Domain domain => domain.ToRpc(),
			DomainTerm term => term.ToRpc(),
			string or null => value,
			IDictionary => value,
			IEnumerable items => items.Cast<object?>().Select( ToRpc ).ToList(),
			_ => value,
		};

	/// <summary>The id of a many2one value (<c>[id, "name"]</c>), or a plain id; null for empty (false).</summary>
	public static int? Many2OneId( object? value )
		=> value switch
		{
			IList { Count: > 0 } list => AsInt( list[ 0 ] ),
			int or long => AsInt( value ),
			_ => null,
		};

	/// <summary>The display name of a many2one value (<c>[id, "name"]</c>); null for empty (false).</summary>
	public static string? Many2OneName( object? value ) => value is IList { Count: > 1 } list ? list[ 1 ] as string : null;

	/// <summary>The ids of a one2many / many2many value (or any list of ids); empty for none.</summary>
	public static IReadOnlyList<int> Ids( object? value )
		=> value is IEnumerable items and not string
			? [ .. items.Cast<object?>().Select( AsInt ).OfType<int>() ]
			: [];

	/// <summary>An integer value, or null for anything else (Odoo's false included).</summary>
	public static int? AsInt( object? value )
		=> value switch
		{
			int i => i,
			long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
			_ => null,
		};

	/// <summary>A string value; null for Odoo's false.</summary>
	public static string? AsString( object? value ) => value as string;

	/// <summary>A row's field as a string (null when missing or false).</summary>
	public static string? Text( this IReadOnlyDictionary<string, object?> row, string field ) => row.GetValueOrDefault( field ) as string;

	/// <summary>A row's integer field (null when missing or false).</summary>
	public static int? Int( this IReadOnlyDictionary<string, object?> row, string field ) => AsInt( row.GetValueOrDefault( field ) );

	/// <summary>A row's boolean field (false when missing).</summary>
	public static bool Bool( this IReadOnlyDictionary<string, object?> row, string field ) => row.GetValueOrDefault( field ) is true;

	/// <summary>A row's many2one id (null when missing or empty).</summary>
	public static int? RefId( this IReadOnlyDictionary<string, object?> row, string field ) => Many2OneId( row.GetValueOrDefault( field ) );

	/// <summary>A row's many2one display name (null when missing or empty).</summary>
	public static string? RefName( this IReadOnlyDictionary<string, object?> row, string field ) => Many2OneName( row.GetValueOrDefault( field ) );

	/// <summary>A row's x2many ids (empty when missing).</summary>
	public static IReadOnlyList<int> RefIds( this IReadOnlyDictionary<string, object?> row, string field ) => Ids( row.GetValueOrDefault( field ) );

	/// <summary>A row's nested object (a dictionary), or null.</summary>
	public static IReadOnlyDictionary<string, object?>? Object( this IReadOnlyDictionary<string, object?> row, string field )
		=> row.GetValueOrDefault( field ) as IReadOnlyDictionary<string, object?>;
}
