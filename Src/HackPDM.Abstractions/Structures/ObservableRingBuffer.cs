using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;

namespace HackPDM.Abstractions.Structures;

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

	public ObservableRingBuffer( int capacity = 10 )
	{
		if( capacity <= 0 )
			throw new ArgumentOutOfRangeException( nameof( capacity ), "Capacity must be greater than zero." );

		Capacity = capacity;
		_buffer = new T[ capacity ];
	}

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

	public int IndexOf( T item )
	{
		for( int i = 0; i < _count; i++ ) {
			if( EqualityComparer<T>.Default.Equals( this[ i ], item ) )
				return i;
		}
		return -1;
	}

	public bool Contains( T item ) => IndexOf( item ) >= 0;
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
	public bool Remove( T item ) => throw new NotSupportedException( "Arbitrary removes are not supported on RingBuffers." );
	public void Insert( int index, T item ) => throw new NotSupportedException( "Arbitrary inserts are not supported on RingBuffers." );
	public void RemoveAt( int index ) => throw new NotSupportedException( "Arbitrary removes are not supported on RingBuffers." );

	#endregion
}