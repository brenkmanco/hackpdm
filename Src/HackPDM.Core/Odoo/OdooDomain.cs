namespace HackPDM.Core.Odoo;

public enum Operators
{
	Equal,
	NotEqual,
	GreaterThan,
	GreaterThanOrEqual,
	LessThan,
	LessThanOrEqual,
	Unset,
	Like,
	LikeEqual,
	NotLike,
	NotILike,
	In,
	NotIn,
	ChildOf,
	ParentOf,
	ILike,
	ILikeEqual,
}

public static class OperatorNames
{
	public static string ToOdoo( this Operators op ) => op switch
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
}

/// <summary>One condition of an Odoo domain: (field, op, value).</summary>
public sealed record DomainTerm( string Field, string Op, object? Value )
{
	public DomainTerm( string field, Operators op, object? value ) : this( field, op.ToOdoo(), value ) { }
	internal object?[] ToRpc() => [ Field, Op, OdooValues.ToRpc( Value ) ];
}

/// <summary>Immutable Odoo prefix-notation search domain.</summary>
public sealed class OdooDomain : IReadOnlyList<object>
{
	public const string AndOp = "&";
	public const string OrOp = "|";
	public const string NotOp = "!";

	private readonly object[] _items;
	private OdooDomain( object[] items ) => _items = items;

	public static OdooDomain All { get; } = new( [] );

	public static OdooDomain Where( string field, string op, object? value ) => All.And( field, op, value );
	public static OdooDomain Where( string field, Operators op, object? value ) => All.And( field, op, value );
	public static OdooDomain Where( string field, object? value ) => All.And( field, "=", value );
	public static OdooDomain IdIn( IEnumerable<int> ids ) => Where( "id", "in", ids.ToArray() );

	public OdooDomain And( string field, string op, object? value ) => new( [ .. _items, new DomainTerm( field, op, value ) ] );
	public OdooDomain And( string field, Operators op, object? value ) => And( field, op.ToOdoo(), value );
	public OdooDomain And( string field, object? value ) => And( field, "=", value );
	public OdooDomain And( OdooDomain other ) => this & other;

	public static OdooDomain operator &( OdooDomain a, OdooDomain b )
		=> a.IsEmpty ? b : b.IsEmpty ? a : new( [ AndOp, .. a.AsExpression(), .. b.AsExpression() ] );

	public static OdooDomain operator |( OdooDomain a, OdooDomain b )
		=> a.IsEmpty || b.IsEmpty ? All : new( [ OrOp, .. a.AsExpression(), .. b.AsExpression() ] );

	public static OdooDomain operator !( OdooDomain a )
		=> a.IsEmpty ? a : new( [ NotOp, .. a.AsExpression() ] );

	public bool IsEmpty => _items.Length == 0;
	public IEnumerable<DomainTerm> Terms => _items.OfType<DomainTerm>();

	private object[] AsExpression()
	{
		int expressions = CountExpressions();
		return expressions <= 1
			? _items
			: [ .. Enumerable.Repeat<object>( AndOp, expressions - 1 ), .. _items ];
	}

	private int CountExpressions()
	{
		int count = 0, need = 0;
		foreach( object item in _items )
		{
			if( need == 0 )
			{
				count++;
				need = 1;
			}
			need += item switch { AndOp or OrOp => 1, NotOp => 0, _ => -1 };
		}
		return count;
	}

	public List<object?> ToRpc()
		=> [ .. _items.Select( item => item is DomainTerm term ? term.ToRpc() : item ) ];

	public int Count => _items.Length;
	public object this[ int index ] => _items[ index ];
	public IEnumerator<object> GetEnumerator() => ( (IEnumerable<object>)_items ).GetEnumerator();
	System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

	public override string ToString()
		=> "[" + string.Join( ", ", _items.Select( i => i is DomainTerm t ? $"('{t.Field}', '{t.Op}', {t.Value})" : $"'{i}'" ) ) + "]";
}
