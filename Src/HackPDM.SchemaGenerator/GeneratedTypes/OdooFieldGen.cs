using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using HackPDM.Shared.GlobalData;

namespace HackPDM.SchemaGenerator.GeneratedTypes;

/// <summary>
/// What the generator needs from one [OdooModel] class. Only strings and value types, so the
/// incremental pipeline can tell an unchanged model from a changed one and skip regenerating it.
/// </summary>
public sealed record OdooModelGen(
	string Name,
	string Namespace,
	string FullName,
	string DbName,
	EquatableArray<OdooPropertyGen> Properties,
	EquatableArray<string> InvalidProperties );

/// <param name="ClrType">The property's type, fully qualified, with <c>?</c> for a nullable reference type.</param>
public sealed record OdooPropertyGen( OdooFieldType OdooType, string OdooFieldName, string PropertyName, string ClrType );

/// <summary>An immutable array compared by its items (ImmutableArray compares by reference).</summary>
public readonly struct EquatableArray<T>( ImmutableArray<T> items ) : IEquatable<EquatableArray<T>>, IEnumerable<T>
	where T : IEquatable<T>
{
	private readonly ImmutableArray<T> _items = items;

	public ImmutableArray<T> Items => _items.IsDefault ? ImmutableArray<T>.Empty : _items;
	public int Count => Items.Length;

	public bool Equals( EquatableArray<T> other ) => Items.SequenceEqual( other.Items );
	public override bool Equals( object? obj ) => obj is EquatableArray<T> other && Equals( other );

	public override int GetHashCode()
	{
		int hash = 17;
		foreach( T item in Items )
			hash = hash * 31 + ( item?.GetHashCode() ?? 0 );
		return hash;
	}

	public IEnumerator<T> GetEnumerator() => ( (IEnumerable<T>)Items ).GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
