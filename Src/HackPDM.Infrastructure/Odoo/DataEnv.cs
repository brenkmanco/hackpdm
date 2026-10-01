using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using HackPDM.Infrastructure.Odoo.Models;

namespace HackPDM.Infrastructure.Odoo;

public class DataEnv<T>
{

}
public interface IRecordSet<T> : IEnumerable<T>, IRecordSetProvider<T> where T : HpBaseModelTransport
{
	int Count { get; }
	T this[ int index ] { get; }
	// Default interface implementation: returns itself
	// Or if GetRecordSet returns IRecordSet<T>:
	IRecordSet<T> IRecordSetProvider<T>.GetRecordSet() => this;
}
public class RecordSet<T> : IEnumerable<T>, IRecordSetProvider<T> where T : HpBaseModelTransport
{
	private readonly List<T> _items;

	public RecordSet( IEnumerable<T> items )
	{
		_items = items?.ToList() ?? new List<T>();
	}

	public int Count => _items.Count;

	public T EnsureOne()
	{
		if( _items.Count != 1 )
			throw new InvalidOperationException( $"Expected 1 record, but set contained {_items.Count}." );
		return _items[ 0 ];
	}

	// Fulfills IRecordSetProvider<T>
	public RecordSet<T> GetRecordSet() => this;

	public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	IRecordSet<T> IRecordSetProvider<T>.GetRecordSet()
	{
		throw new NotImplementedException();
	}
}
public interface IRecordSetProvider<T> where T : HpBaseModelTransport
{
	IRecordSet<T> GetRecordSet();
}
public static class RecordSetExtensions
{
	public static bool Singular<T>( this IRecordSet<T> set ) where T : HpBaseModelTransport => set.Count == 1;
}