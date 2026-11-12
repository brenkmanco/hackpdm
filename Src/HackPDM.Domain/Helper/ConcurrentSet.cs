using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;


namespace HackPDM.Domain.Helper;

/// <summary>A thread-safe set backed by a <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}"/>.</summary>
/// <typeparam name="T">The element type; it must not be <see langword="null"/>.</typeparam>
/// <remarks>
/// Single-item operations are atomic; the set operations (<see cref="IntersectWith"/>, <see cref="SetEquals"/> and so on)
/// are not atomic with respect to concurrent changes. Enumeration order is unspecified.
/// </remarks>
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
	/// <summary>Creates an empty set.</summary>
	public ConcurrentSet()
	{
		_fakeBag = new();
	}

	/// <summary>Creates a set by draining a bag.</summary>
	/// <param name="bag">The bag; its items are taken out of it, leaving it empty.</param>
	public ConcurrentSet( ConcurrentBag<T> bag )
	{
		_fakeBag = new();
		while ( bag.TryTake( out T item ) )
		{
			_fakeBag.TryAdd(item, null);
		}
	}
	/// <summary>Creates a set holding the distinct items of a sequence.</summary>
	/// <param name="items">The items.</param>
	public ConcurrentSet( IEnumerable<T> items )
	{
		_fakeBag = new();
		foreach ( T item in items )
		{
			_fakeBag.TryAdd( item, null );
		}
	}


	/// <summary>Gets the element at a position in the current enumeration order.</summary>
	/// <param name="index">The position.</param>
	/// <value>The element; a linear scan, and positions can change when the set changes.</value>
	/// <exception cref="System.IndexOutOfRangeException"><paramref name="index"/> is negative or not less than <see cref="Count"/>.</exception>
	public T this[int index ]
	{
		get
		{
			if ( index < 0 || index >= _fakeBag.Count )
				throw new IndexOutOfRangeException();
			return _fakeBag.Keys.ElementAt( index );
		}
	}
		


	/// <summary>Adds an item when it isn't in the set yet.</summary>
	/// <param name="item">The item.</param>
	/// <returns><see langword="true"/> when it was added.</returns>
	public bool Add( T item ) => _fakeBag.TryAdd( item, null );
	/// <summary>Adds every item that isn't in the set yet.</summary>
	/// <param name="items">The items.</param>
	public void AddRange( IEnumerable<T> items )
	{
		foreach ( T item in items )
		{
			_fakeBag.TryAdd( item, null );
		}
	}
	/// <summary>Removes and returns some element of the set.</summary>
	/// <param name="item">The removed element, or the default when the set was empty.</param>
	/// <returns><see langword="true"/> when an element was removed.</returns>
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
	/// <summary>Removes every element.</summary>
	public void Clear() => _fakeBag.Clear();
	/// <summary>Tells whether an item is in the set.</summary>
	/// <param name="item">The item.</param>
	/// <returns><see langword="true"/> when it is.</returns>
	public bool Contains( T item ) => _fakeBag.ContainsKey( item );
	public void CopyTo( T [] array, int arrayIndex ) => _fakeBag.Keys.CopyTo( array, arrayIndex );
	/// <summary>Removes every item of a sequence from the set.</summary>
	/// <param name="other">The items to remove.</param>
	public void ExceptWith( IEnumerable<T> other ) 
	{
		foreach (var item in other )
		{
			_fakeBag.TryRemove( item, out _ );
		}
	}
	public IEnumerator<T> GetEnumerator() => _fakeBag.Keys.GetEnumerator();
	/// <summary>Keeps only the elements that are also in a sequence.</summary>
	/// <param name="other">The items to keep.</param>
	/// <remarks>Builds a new backing dictionary and swaps it in; items added concurrently in the meantime are lost.</remarks>
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
	/// <summary>Tells whether every element of this set is in a sequence.</summary>
	/// <param name="other">The sequence; enumerated once per element.</param>
	/// <returns><see langword="true"/> when every element is in <paramref name="other"/>; equal sets also return true, so this is not a strict (proper) subset test.</returns>
	public bool IsProperSubsetOf( IEnumerable<T> other ) => _fakeBag.All( item => other.Contains( item.Key ) );
	/// <summary>Tells whether every item of a sequence is in this set.</summary>
	/// <param name="other">The sequence.</param>
	/// <returns><see langword="true"/> when every item is in this set; equal sets also return true, so this is not a strict (proper) superset test.</returns>
	public bool IsProperSupersetOf( IEnumerable<T> other ) => other.All( item => _fakeBag.ContainsKey( item ) );
	/// <summary>Tells whether every element of this set is in a sequence.</summary>
	/// <param name="other">The sequence.</param>
	/// <returns><see langword="true"/> when every element is in <paramref name="other"/>.</returns>
	public bool IsSubsetOf( IEnumerable<T> other ) => IsProperSubsetOf( other );
	/// <summary>Tells whether every item of a sequence is in this set.</summary>
	/// <param name="other">The sequence.</param>
	/// <returns><see langword="true"/> when every item is in this set.</returns>
	public bool IsSupersetOf( IEnumerable<T> other ) => IsProperSupersetOf( other );
	/// <summary>Tells whether the set and a sequence share an item.</summary>
	/// <param name="other">The sequence.</param>
	/// <returns><see langword="true"/> when at least one item of <paramref name="other"/> is in the set.</returns>
	public bool Overlaps( IEnumerable<T> other ) => other.Any( item => _fakeBag.ContainsKey( item ) );
	/// <summary>Removes an item.</summary>
	/// <param name="item">The item.</param>
	/// <returns><see langword="true"/> when it was in the set.</returns>
	public bool Remove( T item ) => _fakeBag.TryRemove( item, out _ );
	/// <summary>Tells whether the set and a sequence hold the same items.</summary>
	/// <param name="other">The sequence.</param>
	/// <returns>
	/// <see langword="true"/> when the counts match and every item of <paramref name="other"/> is in the set; a sequence with
	/// duplicates therefore compares unequal even if it has the same distinct items.
	/// </returns>
	public bool SetEquals( IEnumerable<T> other ) 
	{
		if ( _fakeBag.Count != other.Count() )
			return false;
		return other.All( item => _fakeBag.ContainsKey( item ) );
	}
	/// <summary>Keeps the elements that are in exactly one of the set and a sequence.</summary>
	/// <param name="other">The sequence; an item repeated in it toggles again each time.</param>
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
	/// <summary>Adds every item of a sequence.</summary>
	/// <param name="other">The items.</param>
	public void UnionWith( IEnumerable<T> other ) => AddRange( other );
	void ICollection<T>.Add( T item ) => Add(item);
	IEnumerator IEnumerable.GetEnumerator() => _fakeBag.Keys.GetEnumerator();

	public static implicit operator ConcurrentSet<T>( T[] items ) => new( items );
	public static implicit operator ConcurrentSet<T>( List<T> items ) => new( items );
	public static implicit operator ConcurrentSet<T>( ConcurrentBag<T> bag ) => new ConcurrentSet<T>(bag);

	#region Dispose
	/// <summary>Releases the backing dictionary; the set can't be used afterwards.</summary>
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