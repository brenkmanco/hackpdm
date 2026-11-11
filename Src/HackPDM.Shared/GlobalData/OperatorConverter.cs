
using System;
using System.Collections.Generic;
using System.Linq;

namespace HackPDM.Shared.GlobalData;

/// <summary>Human-readable byte sizes (B, KB, MB, GB, TB).</summary>
public static class ByteSize
{
	private static readonly string[] Units = [ "B", "KB", "MB", "GB", "TB" ];

	/// <param name="separator">Between the number and the unit (the file list pads it to line up).</param>
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
	public static string LongBytesToString( long bytesize ) => ByteSize.Format( bytesize );

	public static string OperatorToString( Operators op ) => op.OperatorConversion();

	public static Operators StringToOperator( string op )
		=> OpExtension.FromOdoo.TryGetValue( op, out Operators value ) ? value : throw new ArgumentException( "Invalid operator string" );

	public object Convert( object value, Type targetType, object parameter, string language )
		=> value is Operators op ? OperatorToString( op ) : value;
	public object ConvertBack( object value, Type targetType, object parameter, string language )
		=> value is string op ? StringToOperator( op ) : value;
}
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

	public static string OperatorConversion( this Operators op ) => ToOdoo.TryGetValue( op, out string? s ) ? s : op.ToString();

	public static string OperatorConversion( this DomainOperators op )
		=> op switch
		{
			DomainOperators.And => "&",
			DomainOperators.Or => "|",
			DomainOperators.Not => "!",
			_ => throw new ArgumentException(),
		};
}
