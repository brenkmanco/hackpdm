using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;

namespace HackPDM.Abstractions.Structures;

/// <summary>
/// A fixed-capacity list that drops its oldest item when a new one is added while full, and raises collection
/// change notifications so it can be bound to a UI list (for example a status log).
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
public class ObservableRingBuffer<T> : IList<T>, IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged
{
	private readonly T[] _buffer;
	private int _head; // Index of the oldest item (public index 0)
	private int _tail; // Index for the next inserted item
	private int _count;

	public int Capacity { get; }
	public int Count => _count;
	public bool IsReadOnly => false;

	public event NotifyCollectionChangedEventHandler? CollectionChanged;
	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>Creates an empty buffer.</summary>
	/// <param name="capacity">The maximum number of items kept.</param>
	/// <exception cref="System.ArgumentOutOfRangeException"><paramref name="capacity"/> is 0 or less.</exception>
	public ObservableRingBuffer( int capacity = 10 )
	{
		if( capacity <= 0 )
			throw new ArgumentOutOfRangeException( nameof( capacity ), "Capacity must be greater than zero." );

		Capacity = capacity;
		_buffer = new T[ capacity ];
	}

	/// <summary>Gets or replaces the item at a position, where 0 is the oldest item.</summary>
	/// <param name="index">The position, from 0 to <see cref="Count"/> - 1.</param>
	/// <value>The item; setting it raises a Replace notification.</value>
	/// <exception cref="System.ArgumentOutOfRangeException"><paramref name="index"/> is outside the stored items.</exception>
	public T this[ int index ] {
		get {
			ValidateIndex( index );
			return _buffer[ ( _head + index ) % Capacity ]!;
		}
		set {
			ValidateIndex( index );
			int internalIndex = (_head + index) % Capacity;
			T oldItem = _buffer[internalIndex];
			_buffer[ internalIndex ] = value;

			OnCollectionChanged( new NotifyCollectionChangedEventArgs(
				NotifyCollectionChangedAction.Replace, value, oldItem, index ) );
		}
	}

	/// <summary>Appends an item, dropping the oldest one first when the buffer is full.</summary>
	/// <param name="item">The item to append.</param>
	/// <remarks>
	/// When full, raises a Remove notification for index 0 and then an Add notification at the last index;
	/// <see cref="Count"/> does not change, so no Count property notification is raised then.
	/// </remarks>
	public void Add( T item )
	{
		if( _count < Capacity ) {
			int addedIndex = _count;
			_buffer[ _tail ] = item;
			_tail = ( _tail + 1 ) % Capacity;
			_count++;

			OnPropertyChanged( nameof( Count ) );
			OnPropertyChanged( "Item[]" );
			OnCollectionChanged( new NotifyCollectionChangedEventArgs(
				NotifyCollectionChangedAction.Add, item, addedIndex ) );
		} else {
			// Capacity reached: drop oldest item (at _head) and append at _tail
			T removedItem = _buffer[_head];
			_buffer[ _head ] = default!; // Clear reference for GC
			_head = ( _head + 1 ) % Capacity;

			_buffer[ _tail ] = item;
			_tail = ( _tail + 1 ) % Capacity;

			// Notify UI that the item at index 0 was removed, and a new item was added at the end
			OnCollectionChanged( new NotifyCollectionChangedEventArgs(
				NotifyCollectionChangedAction.Remove, removedItem, 0 ) );

			OnPropertyChanged( "Item[]" );
			OnCollectionChanged( new NotifyCollectionChangedEventArgs(
				NotifyCollectionChangedAction.Add, item, Capacity - 1 ) );
		}
	}

	/// <summary>Removes every item and raises a Reset notification.</summary>
	public void Clear()
	{
		Array.Clear( _buffer, 0, Capacity );
		_head = 0;
		_tail = 0;
		_count = 0;

		OnPropertyChanged( nameof( Count ) );
		OnPropertyChanged( "Item[]" );
		OnCollectionChanged( new NotifyCollectionChangedEventArgs( NotifyCollectionChangedAction.Reset ) );
	}

	private void ValidateIndex( int index )
	{
		if( index < 0 || index >= _count )
			throw new ArgumentOutOfRangeException( nameof( index ), "Index was out of range." );
	}

	protected virtual void OnCollectionChanged( NotifyCollectionChangedEventArgs e )
		=> CollectionChanged?.Invoke( this, e );

	protected virtual void OnPropertyChanged( string propertyName )
		=> PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );

	#region IEnumerable & IList Boilerplate
	public IEnumerator<T> GetEnumerator()
	{
		for( int i = 0; i < _count; i++ ) {
			yield return this[ i ];
		}
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	/// <summary>Finds an item's position, oldest first.</summary>
	/// <param name="item">The item, compared with the default equality comparer.</param>
	/// <returns>The position of the first equal item, or -1.</returns>
	public int IndexOf( T item )
	{
		for( int i = 0; i < _count; i++ ) {
			if( EqualityComparer<T>.Default.Equals( this[ i ], item ) )
				return i;
		}
		return -1;
	}

	/// <summary>Tells whether the buffer holds an item.</summary>
	/// <param name="item">The item, compared with the default equality comparer.</param>
	/// <returns><see langword="true"/> when an equal item is stored.</returns>
	public bool Contains( T item ) => IndexOf( item ) >= 0;
	/// <summary>Copies the items, oldest first, into an array.</summary>
	/// <param name="array">The destination.</param>
	/// <param name="arrayIndex">Where in <paramref name="array"/> to start.</param>
	/// <exception cref="System.ArgumentException"><paramref name="array"/> is too short from <paramref name="arrayIndex"/>.</exception>
	/// <exception cref="System.NullReferenceException"><paramref name="array"/> is <see langword="null"/>.</exception>
	public void CopyTo( T[] array, int arrayIndex )
	{
	
		//Except.Run()
		//	.IsNull( array )
		//	.OutOfRange( array, arrayIndex );

		if( array.Length - arrayIndex < _count )
			throw new ArgumentException( "Destination array is not long enough to copy all items." );

		if( _count == 0 )
			return;

		// Segment 1: Copy from _head to the end of the internal buffer (or up to _count)
		int firstSegmentLength = Math.Min(_count, Capacity - _head);
		Array.Copy( _buffer, _head, array, arrayIndex, firstSegmentLength );

		// Segment 2: Copy remaining wrapped items starting from index 0 of internal buffer
		int secondSegmentLength = _count - firstSegmentLength;
		if( secondSegmentLength > 0 ) {
			Array.Copy( _buffer, 0, array, arrayIndex + firstSegmentLength, secondSegmentLength );
		}
	}
	/// <summary>Not supported: a ring buffer only drops its oldest item.</summary>
	/// <param name="item">Ignored.</param>
	/// <returns>Never returns.</returns>
	/// <exception cref="System.NotSupportedException">Always.</exception>
	public bool Remove( T item ) => throw new NotSupportedException( "Arbitrary removes are not supported on RingBuffers." );
	/// <summary>Not supported: items can only be appended.</summary>
	/// <param name="index">Ignored.</param>
	/// <param name="item">Ignored.</param>
	/// <exception cref="System.NotSupportedException">Always.</exception>
	public void Insert( int index, T item ) => throw new NotSupportedException( "Arbitrary inserts are not supported on RingBuffers." );
	/// <summary>Not supported: a ring buffer only drops its oldest item.</summary>
	/// <param name="index">Ignored.</param>
	/// <exception cref="System.NotSupportedException">Always.</exception>
	public void RemoveAt( int index ) => throw new NotSupportedException( "Arbitrary removes are not supported on RingBuffers." );

	#endregion
}