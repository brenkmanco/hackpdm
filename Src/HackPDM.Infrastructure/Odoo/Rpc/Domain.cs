using HackPDM.Shared.GlobalData;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>One condition of an Odoo domain: <c>(field, operator, value)</c>.</summary>
public sealed record DomainTerm( string Field, string Op, object? Value )
{
	public DomainTerm( string field, Operators op, object? value ) : this( field, op.OperatorConversion(), value ) { }

	internal object?[] ToRpc() => [ Field, Op, OdooValues.ToRpc( Value ) ];
}

/// <summary>
/// An Odoo search domain: a list of terms (ANDed) in Odoo's prefix notation, where an item is a
/// <see cref="DomainTerm"/> or one of the operators "&amp;", "|", "!". Immutable; the builders
/// return new domains. <c>a &amp; b</c> and <c>a | b</c> combine two domains.
/// </summary>
public sealed class Domain : IReadOnlyList<object>
{
	public const string AndOp = "&", OrOp = "|", NotOp = "!";

	private readonly object[] _items;

	private Domain( object[] items ) => _items = items;

	/// <summary>Matches every record.</summary>
	public static Domain All { get; } = new( [] );

	public static Domain Where( string field, string op, object? value ) => All.And( field, op, value );
	public static Domain Where( string field, Operators op, object? value ) => All.And( field, op, value );
	/// <summary><c>(field, "=", value)</c>.</summary>
	public static Domain Where( string field, object? value ) => All.And( field, "=", value );
	/// <summary><c>("id", "in", ids)</c>.</summary>
	public static Domain IdIn( IEnumerable<int> ids ) => Where( "id", "in", ids.ToArray() );

	/// <summary>This domain and one more term.</summary>
	public Domain And( string field, string op, object? value ) => new( [ .. _items, new DomainTerm( field, op, value ) ] );
	public Domain And( string field, Operators op, object? value ) => And( field, op.OperatorConversion(), value );
	/// <summary>This domain and <c>(field, "=", value)</c>.</summary>
	public Domain And( string field, object? value ) => And( field, "=", value );
	public Domain And( Domain other ) => this & other;

	/// <summary>Both domains must match.</summary>
	public static Domain operator &( Domain a, Domain b )
		=> a.IsEmpty ? b : b.IsEmpty ? a : new( [ AndOp, .. a.AsExpression(), .. b.AsExpression() ] );

	/// <summary>Either domain matches (an empty side matches everything, so the result does too).</summary>
	public static Domain operator |( Domain a, Domain b )
		=> a.IsEmpty || b.IsEmpty ? All : new( [ OrOp, .. a.AsExpression(), .. b.AsExpression() ] );

	/// <summary>The domain does not match.</summary>
	public static Domain operator !( Domain a ) => a.IsEmpty ? a : new( [ NotOp, .. a.AsExpression() ] );

	public bool IsEmpty => _items.Length == 0;

	/// <summary>The terms only (operators left out).</summary>
	public IEnumerable<DomainTerm> Terms => _items.OfType<DomainTerm>();

	// a top-level list is an implicit AND of its expressions; as one operand it needs explicit "&"s
	private object[] AsExpression()
	{
		int expressions = CountExpressions();
		return expressions <= 1 ? _items : [ .. Enumerable.Repeat<object>( AndOp, expressions - 1 ), .. _items ];
	}

	// number of complete prefix-notation expressions at the top level
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

	/// <summary>What is sent over JSON-RPC: terms as <c>[field, op, value]</c> arrays.</summary>
	public List<object?> ToRpc() => [ .. _items.Select( item => item is DomainTerm term ? term.ToRpc() : item ) ];

	public int Count => _items.Length;
	public object this[ int index ] => _items[ index ];
	public IEnumerator<object> GetEnumerator() => ( (IEnumerable<object>)_items ).GetEnumerator();
	System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

	public override string ToString()
		=> "[" + string.Join( ", ", _items.Select( i => i is DomainTerm t ? $"('{t.Field}', '{t.Op}', {t.Value})" : $"'{i}'" ) ) + "]";
}
