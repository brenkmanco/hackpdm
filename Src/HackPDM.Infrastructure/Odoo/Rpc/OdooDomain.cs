using HackPDM.Shared.GlobalData;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>One condition of an Odoo domain: <c>(field, operator, value)</c>.</summary>
public sealed record DomainTerm( string Field, string Op, object? Value )
{
	/// <summary>Creates a term from a typed operator, converting it to Odoo's operator text (for example <see cref="Operators.ILike"/> to <c>"ilike"</c>).</summary>
	/// <param name="field">The Odoo field name the term tests.</param>
	/// <param name="op">The comparison operator; converted with <see cref="OpExtension.OperatorConversion(Operators)"/>.</param>
	/// <param name="value">The value to compare against. Id lists and other enumerables are sent as JSON arrays.</param>
	public DomainTerm( string field, Operators op, object? value ) : this( field, op.OperatorConversion(), value ) { }

	internal object?[] ToRpc() => [ Field, Op, OdooValues.ToRpc( Value ) ];
}

/// <summary>
/// An Odoo search domain: a list of terms (ANDed) in Odoo's prefix notation, where an item is a
/// <see cref="DomainTerm"/> or one of the operators "&amp;", "|", "!". Immutable; the builders
/// return new domains. <c>a &amp; b</c> and <c>a | b</c> combine two domains.
/// </summary>
/// <remarks>
/// When a multi-term domain is used as one operand of <c>&amp;</c>, <c>|</c> or <c>!</c>, the implicit
/// AND between its terms is made explicit with leading <c>"&amp;"</c> operators so the prefix
/// expression stays correct. Pass the domain itself to <see cref="OdooRpc"/> or
/// <see cref="Repository{T}"/>; <see cref="ToRpc"/> is what goes over the wire.
/// </remarks>
/// <example>
/// <code>
/// // name ilike '%.SLDPRT' AND deleted = false AND (checkout_user = 7 OR checkout_user unset)
/// OdooDomain domain = OdooDomain.Where( "name", Operators.ILike, "%.SLDPRT" )
///     .And( "deleted", false )
///     &amp; ( OdooDomain.Where( "checkout_user", 7 ) | OdooDomain.Where( "checkout_user", false ) );
///
/// Result&lt;HpEntry[]&gt; entries = await Repository&lt;HpEntry&gt;.SearchReadAsync( domain, FieldSelection.Only( "name", "dir_id" ) );
/// </code>
/// </example>
public sealed class OdooDomain : IReadOnlyList<object>
{
	public const string AndOp = "&", OrOp = "|", NotOp = "!";

	private readonly object[] _items;

	private OdooDomain( object[] items ) => _items = items;

	/// <summary>Matches every record.</summary>
	public static OdooDomain All { get; } = new( [] );

	/// <summary>Starts a domain with the single term <c>(field, op, value)</c>, using Odoo's operator text as-is.</summary>
	/// <param name="field">The Odoo field name to test.</param>
	/// <param name="op">Odoo operator text such as <c>"="</c>, <c>"in"</c> or <c>"ilike"</c>; not validated here.</param>
	/// <param name="value">The value to compare against.</param>
	/// <returns>A new one-term domain.</returns>
	public static OdooDomain Where( string field, string op, object? value ) => All.And( field, op, value );
	/// <summary>Starts a domain with the single term <c>(field, op, value)</c>, converting a typed operator to Odoo's text.</summary>
	/// <param name="field">The Odoo field name to test.</param>
	/// <param name="op">The comparison operator.</param>
	/// <param name="value">The value to compare against.</param>
	/// <returns>A new one-term domain.</returns>
	public static OdooDomain Where( string field, Operators op, object? value ) => All.And( field, op, value );
	/// <summary>Starts a domain with the equality term <c>(field, "=", value)</c>.</summary>
	/// <param name="field">The Odoo field name to test.</param>
	/// <param name="value">The value the field must equal. Pass <see langword="false"/> to match an empty (unset) field.</param>
	/// <returns>A new one-term domain.</returns>
	public static OdooDomain Where( string field, object? value ) => All.And( field, "=", value );
	/// <summary>Starts a domain matching records whose id is in <paramref name="ids"/>: <c>("id", "in", ids)</c>.</summary>
	/// <param name="ids">The record ids; enumerated once, immediately.</param>
	/// <returns>A new one-term domain. An empty id list matches no records.</returns>
	public static OdooDomain IdIn( IEnumerable<int> ids ) => Where( "id", "in", ids.ToArray() );

	/// <summary>Returns a copy of this domain with one more term appended (implicitly ANDed).</summary>
	/// <param name="field">The Odoo field name to test.</param>
	/// <param name="op">Odoo operator text such as <c>"="</c> or <c>"in"</c>.</param>
	/// <param name="value">The value to compare against.</param>
	/// <returns>A new domain; this instance is unchanged.</returns>
	public OdooDomain And( string field, string op, object? value ) => new( [ .. _items, new DomainTerm( field, op, value ) ] );
	/// <summary>Returns a copy of this domain with one more term appended, converting a typed operator to Odoo's text.</summary>
	/// <param name="field">The Odoo field name to test.</param>
	/// <param name="op">The comparison operator.</param>
	/// <param name="value">The value to compare against.</param>
	/// <returns>A new domain; this instance is unchanged.</returns>
	public OdooDomain And( string field, Operators op, object? value ) => And( field, op.OperatorConversion(), value );
	/// <summary>Returns a copy of this domain with the equality term <c>(field, "=", value)</c> appended.</summary>
	/// <param name="field">The Odoo field name to test.</param>
	/// <param name="value">The value the field must equal.</param>
	/// <returns>A new domain; this instance is unchanged.</returns>
	public OdooDomain And( string field, object? value ) => And( field, "=", value );
	/// <summary>Combines this domain with another so both must match; same as the <c>&amp;</c> operator.</summary>
	/// <param name="other">The domain that must also match.</param>
	/// <returns>A new domain; an empty side is dropped rather than wrapped.</returns>
	public OdooDomain And( OdooDomain other ) => this & other;

	/// <summary>Combines two domains so both must match, as <c>["&amp;", a, b]</c> in prefix notation.</summary>
	/// <param name="a">The first domain.</param>
	/// <param name="b">The second domain.</param>
	/// <returns>The combined domain. If either side is empty, the other side is returned unchanged.</returns>
	public static OdooDomain operator &( OdooDomain a, OdooDomain b )
		=> a.IsEmpty ? b : b.IsEmpty ? a : new( [ AndOp, .. a.AsExpression(), .. b.AsExpression() ] );

	/// <summary>Combines two domains so either may match, as <c>["|", a, b]</c> in prefix notation.</summary>
	/// <param name="a">The first domain.</param>
	/// <param name="b">The second domain.</param>
	/// <returns>The combined domain. If either side is empty it matches everything, so <see cref="All"/> is returned.</returns>
	public static OdooDomain operator |( OdooDomain a, OdooDomain b )
		=> a.IsEmpty || b.IsEmpty ? All : new( [ OrOp, .. a.AsExpression(), .. b.AsExpression() ] );

	/// <summary>Negates a domain, as <c>["!", a]</c> in prefix notation.</summary>
	/// <param name="a">The domain to negate.</param>
	/// <returns>The negated domain. An empty domain is returned unchanged (it still matches everything, not nothing).</returns>
	public static OdooDomain operator !( OdooDomain a ) => a.IsEmpty ? a : new( [ NotOp, .. a.AsExpression() ] );

	/// <summary>Whether the domain has no items, which Odoo treats as matching every record.</summary>
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

	/// <summary>Builds the value sent over JSON-RPC: terms become <c>[field, op, value]</c> arrays and operators stay as strings.</summary>
	/// <returns>A new list in Odoo's prefix notation; values are converted with <see cref="OdooValues.ToRpc"/>.</returns>
	public List<object?> ToRpc() => [ .. _items.Select( item => item is DomainTerm term ? term.ToRpc() : item ) ];

	public int Count => _items.Length;
	public object this[ int index ] => _items[ index ];
	public IEnumerator<object> GetEnumerator() => ( (IEnumerable<object>)_items ).GetEnumerator();
	System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

	public override string ToString()
		=> "[" + string.Join( ", ", _items.Select( i => i is DomainTerm t ? $"('{t.Field}', '{t.Op}', {t.Value})" : $"'{i}'" ) ) + "]";
}
