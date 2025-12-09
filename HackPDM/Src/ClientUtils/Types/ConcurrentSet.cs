using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HackPDM.Src.ClientUtils.Types;

public class ConcurrentSet<T> : ISet<T>, IReadOnlyCollection<T>, IDisposable
{
	private ConcurrentDictionary<T, bool?> _fakeBag;
	private bool _disposedValue;

	public int Count
	{
		get => _fakeBag.Count;
	}
	public bool IsReadOnly
	{
		get => false;
	}
	public ConcurrentSet()
	{
		_fakeBag = new();
	}

	public ConcurrentSet( ConcurrentBag<T> bag )
	{
		_fakeBag = new();
		while ( bag.TryTake( out T item ) )
		{
			_fakeBag.TryAdd(item, null);
		}
	}
	public ConcurrentSet( IEnumerable<T> items )
	{
		_fakeBag = new();
		foreach ( T item in items )
		{
			_fakeBag.TryAdd( item, null );
		}
	}


	public T this[int index ]
	{
		get
		{
			if ( index < 0 || index >= _fakeBag.Count )
				throw new IndexOutOfRangeException();
			return _fakeBag.Keys.ElementAt( index );
		}
	}
		


	public bool Add( T item ) => _fakeBag.TryAdd( item, null );
	public void AddRange( IEnumerable<T> items )
	{
		foreach ( T item in items )
		{
			_fakeBag.TryAdd( item, null );
		}
	}
	public bool TryTake( out T item )
	{
		foreach ( var key in _fakeBag.Keys )
		{
			if ( _fakeBag.TryRemove( key, out _ ) )
			{
				item = key;
				return true;
			}
		}
		item = default;
		return false;
	}
	public void Clear() => _fakeBag.Clear();
	public bool Contains( T item ) => _fakeBag.ContainsKey( item );
	public void CopyTo( T [] array, int arrayIndex ) => _fakeBag.Keys.CopyTo( array, arrayIndex );
	public void ExceptWith( IEnumerable<T> other ) 
	{
		foreach (var item in other )
		{
			_fakeBag.TryRemove( item, out _ );
		}
	}
	public IEnumerator<T> GetEnumerator() => _fakeBag.Keys.GetEnumerator();
	public void IntersectWith( IEnumerable<T> other ) 
	{
		ConcurrentDictionary<T, bool?> newBag = new();
		foreach (T item in other)
		{
			if (_fakeBag.ContainsKey( item ))
			{
				newBag.TryAdd(item, null);
			}
		}
		_fakeBag = newBag;
	}
	public bool IsProperSubsetOf( IEnumerable<T> other ) => _fakeBag.All( item => other.Contains( item.Key ) );
	public bool IsProperSupersetOf( IEnumerable<T> other ) => other.All( item => _fakeBag.ContainsKey( item ) );
	public bool IsSubsetOf( IEnumerable<T> other ) => IsProperSubsetOf( other );
	public bool IsSupersetOf( IEnumerable<T> other ) => IsProperSupersetOf( other );
	public bool Overlaps( IEnumerable<T> other ) => other.Any( item => _fakeBag.ContainsKey( item ) );
	public bool Remove( T item ) => _fakeBag.TryRemove( item, out _ );
	public bool SetEquals( IEnumerable<T> other ) 
	{
		if ( _fakeBag.Count != other.Count() )
			return false;
		return other.All( item => _fakeBag.ContainsKey( item ) );
	}
	public void SymmetricExceptWith( IEnumerable<T> other )
	{
		foreach ( T item in other )
		{
			if (!_fakeBag.TryRemove(item, out _))
			{
				_fakeBag.TryAdd( item, null );
			}
		}
	}
	public void UnionWith( IEnumerable<T> other ) => AddRange( other );
	void ICollection<T>.Add( T item ) => Add(item);
	IEnumerator IEnumerable.GetEnumerator() => _fakeBag.Keys.GetEnumerator();

	public static implicit operator ConcurrentSet<T>( T[] items ) => new( items );
	public static implicit operator ConcurrentSet<T>( List<T> items ) => new( items );
	public static implicit operator ConcurrentSet<T>( ConcurrentBag<T> bag ) => new ConcurrentSet<T>(bag);

	#region Dispose
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose( disposing: true );
		GC.SuppressFinalize( this );
	}
	protected virtual void Dispose( bool disposing )
	{
		if ( !_disposedValue )
		{
			if ( disposing )
			{
				// TODO: dispose managed state (managed objects)
				_fakeBag = null;
			}

			// TODO: free unmanaged resources (unmanaged objects) and override finalizer
			// TODO: set large fields to null
			_disposedValue= true;
		}
	}

	// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
	~ConcurrentSet()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose( disposing: false );
	}
	#endregion
}