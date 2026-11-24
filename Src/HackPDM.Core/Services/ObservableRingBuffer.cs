using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace HackPDM.Core.Services;

/// <summary>
/// A fixed-capacity circular list that drops its oldest item in O(1) without shifting or reallocating
/// elements once at capacity, supporting both oldest-first and newest-first (reverse) ordering for UI binding.
/// </summary>
public sealed class ObservableRingBuffer<T> : IList<T>, IReadOnlyList<T>, IList, INotifyCollectionChanged, INotifyPropertyChanged
{
	private const int InitialAllocation = 16;
	private const int ShrinkClearThreshold = 4096;
	private const int IncrementalBatchLimit = 10;

	private static readonly PropertyChangedEventArgs CountChangedArgs = new( nameof( Count ) );
	private static readonly PropertyChangedEventArgs CapacityChangedArgs = new( nameof( Capacity ) );
	private static readonly PropertyChangedEventArgs IndexerChangedArgs = new( "Item[]" );
	private static readonly NotifyCollectionChangedEventArgs ResetChangedArgs = new( NotifyCollectionChangedAction.Reset );

	private readonly object _gate = new();
	private T[] _buffer = [];
	private int _head; // Physical index of logical index 0
	private int _count;
	private int _capacity;

	public int Capacity
	{
		get { lock( _gate ) return _capacity; }
	}

	public int Count
	{
		get { lock( _gate ) return _count; }
	}

	public bool NewestFirst { get; }
	public bool IsReadOnly => false;
	bool IList.IsFixedSize => false;
	bool ICollection.IsSynchronized => true;
	object ICollection.SyncRoot => _gate;

	public event NotifyCollectionChangedEventHandler? CollectionChanged;
	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>Creates an empty ring buffer.</summary>
	/// <param name="capacity">Maximum number of items kept before overwriting the oldest.</param>
	/// <param name="newestFirst">When true, index 0 is the newest item (reverse / bottom-to-top log order) in O(1) without array shifts.</param>
	public ObservableRingBuffer( int capacity = 10, bool newestFirst = false )
	{
		if( capacity <= 0 )
			throw new ArgumentOutOfRangeException( nameof( capacity ), "Capacity must be greater than zero." );

		_capacity = capacity;
		NewestFirst = newestFirst;
	}

	public T this[ int index ]
	{
		get
		{
			lock( _gate )
			{
				ValidateIndex( index );
				return _buffer[ PhysicalIndex( index ) ]!;
			}
		}
		set
		{
			lock( _gate )
			{
				ValidateIndex( index );
				int physical = PhysicalIndex( index );
				T oldItem = _buffer[ physical ];
				_buffer[ physical ] = value;

				OnPropertyChanged( IndexerChangedArgs );
				OnCollectionChanged( new NotifyCollectionChangedEventArgs(
					NotifyCollectionChangedAction.Replace, value, oldItem, index ) );
			}
		}
	}

	/// <summary>Inserts an item in O(1), dropping the oldest item first when at capacity.</summary>
	public void Add( T item )
	{
		lock( _gate )
		{
			if( _count < _capacity )
			{
				EnsureBufferFor( _count + 1 );
				int addedIndex;
				if( NewestFirst )
				{
					_head = _head == 0 ? _buffer.Length - 1 : _head - 1;
					_buffer[ _head ] = item;
					addedIndex = 0;
				}
				else
				{
					addedIndex = _count;
					_buffer[ PhysicalIndex( addedIndex ) ] = item;
				}
				_count++;

				OnPropertyChanged( CountChangedArgs );
				OnPropertyChanged( IndexerChangedArgs );
				OnCollectionChanged( new NotifyCollectionChangedEventArgs(
					NotifyCollectionChangedAction.Add, item, addedIndex ) );
				return;
			}

			// Capacity reached: evict oldest first so Count stays consistent during Remove and Add events.
			if( NewestFirst )
			{
				int slot = _head == 0 ? _buffer.Length - 1 : _head - 1;
				T removed = _buffer[ slot ];
				_buffer[ slot ] = default!;
				_count--;
				OnCollectionChanged( new NotifyCollectionChangedEventArgs(
					NotifyCollectionChangedAction.Remove, removed, _count ) );

				_head = slot;
				_buffer[ _head ] = item;
				_count++;
				OnPropertyChanged( IndexerChangedArgs );
				OnCollectionChanged( new NotifyCollectionChangedEventArgs(
					NotifyCollectionChangedAction.Add, item, 0 ) );
			}
			else
			{
				T removed = _buffer[ _head ];
				_buffer[ _head ] = default!;
				_head = _head + 1 == _buffer.Length ? 0 : _head + 1;
				_count--;
				OnCollectionChanged( new NotifyCollectionChangedEventArgs(
					NotifyCollectionChangedAction.Remove, removed, 0 ) );

				int tail = PhysicalIndex( _count );
				_buffer[ tail ] = item;
				_count++;
				OnPropertyChanged( IndexerChangedArgs );
				OnCollectionChanged( new NotifyCollectionChangedEventArgs(
					NotifyCollectionChangedAction.Add, item, _count - 1 ) );
			}
		}
	}

	/// <summary>
	/// Adds a chronological batch of items (<paramref name="items"/>[0] oldest, last newest).
	/// Small batches notify incrementally; larger bursts update the ring in place and raise one Reset.
	/// </summary>
	public void AddRange( IReadOnlyList<T> items )
	{
		ArgumentNullException.ThrowIfNull( items );
		lock( _gate )
		{
			int batchCount = items.Count;
			if( batchCount == 0 )
				return;
			if( batchCount <= IncrementalBatchLimit && batchCount < _capacity )
			{
				for( int i = 0; i < batchCount; i++ )
					Add( items[ i ] );
				return;
			}

			if( batchCount >= _capacity )
			{
				EnsureBufferFor( _capacity );
				int start = batchCount - _capacity;
				if( NewestFirst )
				{
					for( int i = 0; i < _capacity; i++ )
						_buffer[ i ] = items[ batchCount - 1 - i ];
				}
				else
				{
					for( int i = 0; i < _capacity; i++ )
						_buffer[ i ] = items[ start + i ];
				}
				_head = 0;
				_count = _capacity;
			}
			else
			{
				int nextCount = Math.Min( _count + batchCount, _capacity );
				EnsureBufferFor( nextCount );
				int len = _buffer.Length;
				if( NewestFirst )
				{
					for( int i = 0; i < batchCount; i++ )
					{
						_head = _head == 0 ? len - 1 : _head - 1;
						_buffer[ _head ] = items[ i ];
						if( _count < _capacity )
							_count++;
					}
				}
				else
				{
					for( int i = 0; i < batchCount; i++ )
					{
						if( _count < _capacity )
						{
							_buffer[ PhysicalIndex( _count ) ] = items[ i ];
							_count++;
						}
						else
						{
							_buffer[ _head ] = items[ i ];
							_head = _head + 1 == len ? 0 : _head + 1;
						}
					}
				}
			}

			NotifyReset();
		}
	}

	/// <summary>Removes every item and raises a Reset notification if non-empty.</summary>
	public void Clear()
	{
		lock( _gate )
		{
			if( _count == 0 )
				return;
			if( _buffer.Length > ShrinkClearThreshold )
				_buffer = [];
			else
				Array.Clear( _buffer, 0, _buffer.Length );
			_head = 0;
			_count = 0;
			NotifyReset();
		}
	}

	/// <summary>Changes capacity, retaining up to <paramref name="newCapacity"/> newest items in their current display order.</summary>
	public void Resize( int newCapacity )
	{
		if( newCapacity <= 0 )
			throw new ArgumentOutOfRangeException( nameof( newCapacity ), "Capacity must be greater than zero." );
		lock( _gate )
		{
			if( newCapacity == _capacity )
				return;

			int previousCount = _count;
			if( _count == 0 )
			{
				if( _buffer.Length > newCapacity )
					_buffer = [];
				_capacity = newCapacity;
				OnPropertyChanged( CapacityChangedArgs );
				return;
			}

			if( _count > newCapacity || _buffer.Length > newCapacity )
			{
				int keep = Math.Min( _count, newCapacity );
				int startOffset = NewestFirst ? 0 : _count - keep;
				T[] next = new T[ newCapacity ];
				CopyToCore( next, 0, startOffset, keep );
				_buffer = next;
				_head = 0;
				_count = keep;
			}

			_capacity = newCapacity;
			OnPropertyChanged( CapacityChangedArgs );

			if( _count != previousCount )
				NotifyReset();
		}
	}

	private int PhysicalIndex( int logicalIndex )
	{
		int raw = _head + logicalIndex;
		int len = _buffer.Length;
		return raw >= len ? raw - len : raw;
	}

	private void EnsureBufferFor( int needed )
	{
		int target = Math.Min( needed, _capacity );
		if( _buffer.Length >= target )
			return;

		int nextLength = _buffer.Length == 0
			? InitialAllocation
			: ( _buffer.Length > _capacity / 2 ? _capacity : _buffer.Length * 2 );
		if( nextLength < target )
			nextLength = target;
		if( nextLength > _capacity )
			nextLength = _capacity;

		T[] next = new T[ nextLength ];
		if( _count > 0 )
			CopyToCore( next, 0, 0, _count );
		_buffer = next;
		_head = 0;
	}

	private void CopyToCore( T[] destination, int destIndex, int logicalStart, int length )
	{
		if( length <= 0 )
			return;
		int start = PhysicalIndex( logicalStart );
		int first = Math.Min( length, _buffer.Length - start );
		Array.Copy( _buffer, start, destination, destIndex, first );
		int remaining = length - first;
		if( remaining > 0 )
			Array.Copy( _buffer, 0, destination, destIndex + first, remaining );
	}

	private void ValidateIndex( int index )
	{
		if( (uint)index >= (uint)_count )
			throw new ArgumentOutOfRangeException( nameof( index ), "Index was out of range." );
	}

	private void NotifyReset()
	{
		OnPropertyChanged( CountChangedArgs );
		OnPropertyChanged( IndexerChangedArgs );
		OnCollectionChanged( ResetChangedArgs );
	}

	private void OnCollectionChanged( NotifyCollectionChangedEventArgs e )
		=> CollectionChanged?.Invoke( this, e );

	private void OnPropertyChanged( PropertyChangedEventArgs e )
		=> PropertyChanged?.Invoke( this, e );

	public IEnumerator<T> GetEnumerator()
	{
		T[] snapshot;
		lock( _gate )
		{
			if( _count == 0 )
				return Enumerable.Empty<T>().GetEnumerator();
			snapshot = new T[ _count ];
			CopyToCore( snapshot, 0, 0, _count );
		}
		return ( (IEnumerable<T>)snapshot ).GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public int IndexOf( T item )
	{
		EqualityComparer<T> comparer = EqualityComparer<T>.Default;
		lock( _gate )
		{
			for( int i = 0; i < _count; i++ )
			{
				if( comparer.Equals( _buffer[ PhysicalIndex( i ) ], item ) )
					return i;
			}
			return -1;
		}
	}

	public bool Contains( T item ) => IndexOf( item ) >= 0;

	public void CopyTo( T[] array, int arrayIndex )
	{
		ArgumentNullException.ThrowIfNull( array );
		ArgumentOutOfRangeException.ThrowIfNegative( arrayIndex );
		lock( _gate )
		{
			if( array.Length - arrayIndex < _count )
				throw new ArgumentException( "Destination array is not long enough to copy all items.", nameof( array ) );

			CopyToCore( array, arrayIndex, 0, _count );
		}
	}

	public bool Remove( T item ) => throw new NotSupportedException( "Arbitrary removes are not supported on RingBuffers." );
	public void Insert( int index, T item ) => throw new NotSupportedException( "Arbitrary inserts are not supported on RingBuffers." );
	public void RemoveAt( int index ) => throw new NotSupportedException( "Arbitrary removes are not supported on RingBuffers." );

	object? IList.this[ int index ]
	{
		get => this[ index ];
		set => this[ index ] = (T)value!;
	}

	int IList.Add( object? value )
	{
		lock( _gate )
		{
			Add( (T)value! );
			return NewestFirst ? 0 : _count - 1;
		}
	}

	bool IList.Contains( object? value ) => value is T item && Contains( item );
	int IList.IndexOf( object? value ) => value is T item ? IndexOf( item ) : -1;
	void IList.Insert( int index, object? value ) => Insert( index, (T)value! );
	void IList.Remove( object? value ) => throw new NotSupportedException( "Arbitrary removes are not supported on RingBuffers." );
	void IList.RemoveAt( int index ) => RemoveAt( index );

	void ICollection.CopyTo( Array array, int index )
	{
		ArgumentNullException.ThrowIfNull( array );
		ArgumentOutOfRangeException.ThrowIfNegative( index );
		if( array is T[] typed )
		{
			CopyTo( typed, index );
			return;
		}
		lock( _gate )
		{
			if( array.Length - index < _count )
				throw new ArgumentException( "Destination array is not long enough to copy all items.", nameof( array ) );
			for( int i = 0; i < _count; i++ )
				array.SetValue( _buffer[ PhysicalIndex( i ) ], index + i );
		}
	}
}
