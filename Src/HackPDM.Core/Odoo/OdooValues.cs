using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using HackPDM.Core.Odoo.Models;

namespace HackPDM.Core.Odoo;

/// <summary>Outcome of an RPC or transfer operation: either a value or an error message.</summary>
public readonly record struct Result<T>( T? Value, string? Error )
{
	[MemberNotNullWhen( false, nameof( Error ) )]
	public bool Ok => Error is null;

	public static Result<T> Success( T value ) => new( value, null );
	public static Result<T> Fail( string error ) => new( default, string.IsNullOrEmpty( error ) ? "unknown error" : error );

	public T ValueOr( T fallback ) => Ok && Value is not null ? Value : fallback;
	public T Unwrap() => Ok ? Value! : throw new OdooException( Error );
	public Result<TOut> Map<TOut>( Func<T, TOut> map ) => Ok ? Result<TOut>.Success( map( Value! ) ) : Result<TOut>.Fail( Error );

	public override string ToString() => Ok ? $"Ok({Value})" : $"Fail({Error})";
}

public sealed class OdooException( string message ) : Exception( message );

/// <summary>A single row returned by Odoo read / search_read.</summary>
public sealed class OdooRow : Dictionary<string, object?>
{
	public OdooRow() { }
	public OdooRow( IDictionary<string, object?> values ) : base( values ) { }
	public int Id => this.Int( "id" ) ?? 0;
}

/// <summary>Typed conversions between Odoo JSON-RPC values and .NET types (zero reflection).</summary>
public static class OdooValues
{
	public static object? FromJson( JsonElement element )
		=> element.ValueKind switch
		{
			JsonValueKind.Object => element.EnumerateObject().ToDictionary( p => p.Name, p => FromJson( p.Value ) ),
			JsonValueKind.Array => element.EnumerateArray().Select( FromJson ).ToList(),
			JsonValueKind.String => element.GetString(),
			JsonValueKind.Number => element.TryGetInt32( out int i ) ? i : element.TryGetInt64( out long l ) ? (object)l : element.GetDouble(),
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			JsonValueKind.Null or JsonValueKind.Undefined => null,
			_ => element.GetRawText(),
		};

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

	// Inbound typed readers (Odoo sends `false` for empty non-boolean fields)
	public static string? ReadString( object? value )
		=> value is null or false ? null : value as string ?? value.ToString();

	public static int? ReadInt( object? value )
		=> value is null or false ? null : value switch
		{
			int i => i,
			long l => (int)l,
			_ => 0,
		};

	public static float? ReadFloat( object? value )
		=> value is null or false ? null : value switch
		{
			float f => f,
			double d => (float)d,
			decimal m => (float)m,
			int i => i,
			long l => l,
			_ => 0f,
		};

	public static bool? ReadBool( object? value )
		=> value is null ? null : value is true;

	public static DateTime? ReadDateTime( object? value )
		=> value is null or false ? null : value switch
		{
			DateTime dt => dt,
			string text when DateTime.TryParse( text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed ) => parsed,
			string text when DateTime.TryParse( text, out DateTime parsed ) => parsed,
			_ => default( DateTime ),
		};

	public static Many2One? ReadMany2One( object? value )
	{
		if( value is null or false )
			return null;
		return value is IList { Count: >= 2 } pair
			? new Many2One { id = AsInt( pair[ 0 ] ) ?? 0, name = pair[ 1 ] as string ?? "" }
			: new Many2One { id = 0, name = "" };
	}

	public static One2Many? ReadOne2Many( object? value )
		=> value is null or false ? null : new One2Many( Ids( value ) );

	public static Many2Many? ReadMany2Many( object? value )
		=> value is null or false ? null : new Many2Many( Ids( value ) );

	public static byte[]? ReadBytes( object? value )
	{
		if( value is null or false )
			return null;
		if( value is byte[] bytes )
			return bytes;
		if( value is not string { Length: > 0 } text )
			return null;
		try { return Convert.FromBase64String( text ); }
		catch( FormatException ) { return null; }
	}

	public static Dictionary<string, object?>? ReadJsonObject( object? value )
	{
		if( value is null or false )
			return null;
		if( value is Dictionary<string, object?> dict )
			return dict;
		if( value is IDictionary raw )
		{
			Dictionary<string, object?> copy = [];
			foreach( DictionaryEntry entry in raw )
				copy[ entry.Key.ToString() ?? "" ] = entry.Value;
			return copy;
		}
		if( value is string { Length: > 0 } text )
		{
			using JsonDocument doc = JsonDocument.Parse( text );
			return FromJson( doc.RootElement ) as Dictionary<string, object?> ?? [];
		}
		return [];
	}

	// Outbound helpers
	public static object? OutBool( bool? value ) => value is true ? 1 : value is false ? 0 : null;
	public static object? OutFloat( float? value ) => value is null ? null : Convert.ToDouble( value.Value );
	public static string? OutDateTime( DateTime? value ) => value?.ToString( "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture );
	public static string? OutBytes( byte[]? value ) => value is null ? null : Convert.ToBase64String( value );
	public static object? OutX2Many( MultiRecord? value )
		=> value is null ? null : new object?[] { new object?[] { 6, 0, value.IdValues } };

	// Row helpers
	public static int? Many2OneId( object? value )
		=> value switch
		{
			IList { Count: > 0 } list => AsInt( list[ 0 ] ),
			int or long => AsInt( value ),
			_ => null,
		};

	public static string? Many2OneName( object? value )
		=> value is IList { Count: > 1 } list ? list[ 1 ] as string : null;

	public static IReadOnlyList<int> Ids( object? value )
		=> value is IEnumerable items and not string
			? [ .. items.Cast<object?>().Select( AsInt ).OfType<int>() ]
			: [];

	public static int? AsInt( object? value )
		=> value switch
		{
			int i => i,
			long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
			_ => null,
		};

	public static string? AsString( object? value ) => value as string;

	public static string? Text( this IReadOnlyDictionary<string, object?> row, string field )
		=> row.GetValueOrDefault( field ) as string;

	public static int? Int( this IReadOnlyDictionary<string, object?> row, string field )
		=> AsInt( row.GetValueOrDefault( field ) );

	public static bool Bool( this IReadOnlyDictionary<string, object?> row, string field )
		=> row.GetValueOrDefault( field ) is true;

	public static int? RefId( this IReadOnlyDictionary<string, object?> row, string field )
		=> Many2OneId( row.GetValueOrDefault( field ) );

	public static string? RefName( this IReadOnlyDictionary<string, object?> row, string field )
		=> Many2OneName( row.GetValueOrDefault( field ) );

	public static IReadOnlyList<int> RefIds( this IReadOnlyDictionary<string, object?> row, string field )
		=> Ids( row.GetValueOrDefault( field ) );

	public static IReadOnlyDictionary<string, object?>? Object( this IReadOnlyDictionary<string, object?> row, string field )
		=> row.GetValueOrDefault( field ) as IReadOnlyDictionary<string, object?>;
}
