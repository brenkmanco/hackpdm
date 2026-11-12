
using System;
using System.Collections.Generic;
using System.Linq;

namespace HackPDM.Shared.GlobalData;

/// <summary>Human-readable byte sizes (B, KB, MB, GB, TB).</summary>
public static class ByteSize
{
	private static readonly string[] Units = [ "B", "KB", "MB", "GB", "TB" ];

	/// <summary>Formats a byte count with the largest binary unit (1024-based) that keeps the number at least 1.</summary>
	/// <param name="bytes">The size in bytes.</param>
	/// <param name="separator">Between the number and the unit (the file list pads it to line up).</param>
	/// <returns>
	/// For example "512 B" or "1.5 MB", with up to two decimals. Values below 1 KB, above 1 PB, or negative are
	/// shown in bytes.
	/// </returns>
	public static string Format( long bytes, string separator = " " )
	{
		if( bytes is < 1024 or > 1125899906842624 )
			return $"{bytes}{separator}B";
		int unit = 0;
		double value = bytes;
		while( value >= 1024 && unit < Units.Length - 1 )
		{
			value /= 1024;
			unit++;
		}
		return $"{value:.##}{separator}{Units[ unit ]}";
	}
}

public partial class OperatorConverter
{
	/// <summary>Formats a byte count for display; see <see cref="ByteSize.Format"/>.</summary>
	/// <param name="bytesize">The size in bytes.</param>
	/// <returns>The size with a unit, separated by a space.</returns>
	public static string LongBytesToString( long bytesize ) => ByteSize.Format( bytesize );

	/// <summary>Gets the Odoo domain spelling of an operator.</summary>
	/// <param name="op">The operator.</param>
	/// <returns>For example "=", "ilike" or "child_of"; the enum name for an operator without a mapping.</returns>
	public static string OperatorToString( Operators op ) => op.OperatorConversion();

	/// <summary>Parses an Odoo domain operator.</summary>
	/// <param name="op">The Odoo spelling, for example "!=" or "not ilike" (exact, case-sensitive).</param>
	/// <returns>The matching <see cref="Operators"/> value.</returns>
	/// <exception cref="System.ArgumentException"><paramref name="op"/> is not a known operator.</exception>
	public static Operators StringToOperator( string op )
		=> OpExtension.FromOdoo.TryGetValue( op, out Operators value ) ? value : throw new ArgumentException( "Invalid operator string" );

	/// <summary>Value-converter hook for bindings: shows an <see cref="Operators"/> value as its Odoo spelling.</summary>
	/// <param name="value">The bound value.</param>
	/// <param name="targetType">Unused.</param>
	/// <param name="parameter">Unused.</param>
	/// <param name="language">Unused.</param>
	/// <returns>The operator's spelling, or <paramref name="value"/> unchanged when it is not an operator.</returns>
	public object Convert( object value, Type targetType, object parameter, string language )
		=> value is Operators op ? OperatorToString( op ) : value;
	/// <summary>Value-converter hook for bindings: turns an Odoo operator spelling back into an <see cref="Operators"/> value.</summary>
	/// <param name="value">The bound value.</param>
	/// <param name="targetType">Unused.</param>
	/// <param name="parameter">Unused.</param>
	/// <param name="language">Unused.</param>
	/// <returns>The operator, or <paramref name="value"/> unchanged when it is not a string.</returns>
	/// <exception cref="System.ArgumentException"><paramref name="value"/> is a string that is not a known operator.</exception>
	public object ConvertBack( object value, Type targetType, object parameter, string language )
		=> value is string op ? StringToOperator( op ) : value;
}
/// <summary>Conversions between the operator enums and their Odoo domain spellings.</summary>
public static class OpExtension
{
	/// <summary>The one operator table: each operator and its Odoo domain spelling.</summary>
	private static readonly Dictionary<Operators, string> ToOdoo = new()
	{
		[ Operators.Equal ] = "=",
		[ Operators.NotEqual ] = "!=",
		[ Operators.GreaterThan ] = ">",
		[ Operators.GreaterThanOrEqual ] = ">=",
		[ Operators.LessThan ] = "<",
		[ Operators.LessThanOrEqual ] = "<=",
		[ Operators.Unset ] = "is null",
		[ Operators.Like ] = "like",
		[ Operators.LikeEqual ] = "=like",
		[ Operators.NotLike ] = "not like",
		[ Operators.NotILike ] = "not ilike",
		[ Operators.In ] = "in",
		[ Operators.NotIn ] = "not in",
		[ Operators.ChildOf ] = "child_of",
		[ Operators.ParentOf ] = "parent_of",
		[ Operators.ILike ] = "ilike",
		[ Operators.ILikeEqual ] = "=ilike",
	};
	internal static readonly Dictionary<string, Operators> FromOdoo = ToOdoo.ToDictionary( p => p.Value, p => p.Key );

	/// <summary>Gets the Odoo domain spelling of a comparison operator.</summary>
	/// <param name="op">The operator.</param>
	/// <returns>For example "=", "ilike" or "child_of"; the enum name for an operator without a mapping.</returns>
	public static string OperatorConversion( this Operators op ) => ToOdoo.TryGetValue( op, out string? s ) ? s : op.ToString();

	/// <summary>Gets the Odoo prefix (Polish notation) symbol of a logical domain operator.</summary>
	/// <param name="op">The logical operator.</param>
	/// <returns>"&amp;" for And, "|" for Or, "!" for Not.</returns>
	/// <exception cref="System.ArgumentException"><paramref name="op"/> is not And, Or or Not.</exception>
	public static string OperatorConversion( this DomainOperators op )
		=> op switch
		{
			DomainOperators.And => "&",
			DomainOperators.Or => "|",
			DomainOperators.Not => "!",
			_ => throw new ArgumentException(),
		};
}
