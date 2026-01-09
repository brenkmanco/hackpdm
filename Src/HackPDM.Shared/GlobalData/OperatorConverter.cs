
namespace HackPDM.Shared.GlobalData;

public partial class OperatorConverter
{
	public static string LongBytesToString(long bytesize)
	{
		return bytesize switch
		{
			< 1024 => $"{bytesize} B",
			< 1048576 => $"{bytesize / 1024f:.##} KB",
			< 1073741824 => $"{bytesize / 1048576f:.##}   MB",
			< 1099511627776 => $"{bytesize / 1073741824f:.##} GB",
			<= 1125899906842624 => $"{bytesize / 1099511627776f:.##} TB",
			_ => $"{bytesize} B",
		};
	}

    public static string OperatorToString(Operators op)
        => op.OperatorConversion();
	public static Operators StringToOperator(string op)
		=> op switch
		{
			"=" => Operators.Equal,
			"!=" => Operators.NotEqual,
			">" => Operators.GreaterThan,
			">=" => Operators.GreaterThanOrEqual,
			"<" => Operators.LessThan,
			"<=" => Operators.LessThanOrEqual,
			"is null" => Operators.Unset,
			"like" => Operators.Like,
			"=like" => Operators.LikeEqual,
			"not like" => Operators.NotLike,
			"not ilike" => Operators.NotILike,
			"in" => Operators.In,
			"not in" => Operators.NotIn,
			"child_of" => Operators.ChildOf,
			"parent_of" => Operators.ParentOf,
			"ilike" => Operators.ILike,
			"=ilike" => Operators.ILikeEqual,
			_ => throw new ArgumentException("Invalid operator string"),
		};
	public object Convert(object value, Type targetType, object parameter, string language)
		=> value is Operators op
			? OperatorToString(op)
			: value;
	public object ConvertBack(object value, Type targetType, object parameter, string language)
		=> value is string op
			? StringToOperator(op) : value;
}
public static class OpExtension
{
	public static string OperatorConversion(this Operators op)
		=> op switch
		{
			Operators.Equal => "=",
			Operators.NotEqual => "!=",
			Operators.GreaterThan => ">",
			Operators.GreaterThanOrEqual => ">=",
			Operators.LessThan => "<",
			Operators.LessThanOrEqual => "<=",
			Operators.Unset => "is null",
			Operators.Like => "like",
			Operators.LikeEqual => "=like",
			Operators.NotLike => "not like",
			Operators.NotILike => "not ilike",
			Operators.In => "in",
			Operators.NotIn => "not in",
			Operators.ChildOf => "child_of",
			Operators.ParentOf => "parent_of",
			Operators.ILike => "ilike",
			Operators.ILikeEqual => "=ilike",
			_ => op.ToString(),
		};
	public static string OperatorConversion(this DomainOperators op)
	{
		return op switch
		{
			DomainOperators.And => "&",
			DomainOperators.Or => "|",
			DomainOperators.Not => "!",
			_ => throw new ArgumentException(),
		};
	}
}