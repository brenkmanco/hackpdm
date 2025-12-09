using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HackPDM.Src.ClientUtils.Types;

public class IndexedQueue<T> : IEnumerable<T>, IEnumerable, ICollection<T>, ICloneable
{
    public int Start { get; set; }
    public int Count { get; private set; }
    public int Capacity { get; private set; }
    public int InitialCapacity { get; set; }
    public bool Growable 
    { 
        get; 
        set; 
    }
    public int MaxCapacity 
    {
        get
        {
            if (field is 0)
            {
                field = int.MaxValue;
            }
            return field;
        }
        set
        {
            if (value < Capacity) throw new ArgumentException("MaxCapacity cannot be less than current capacity");
            field = value;
        }
    }
    public object SyncRoot => _buffer.SyncRoot;
    public bool IsSynchronized => _buffer.IsSynchronized;
    public bool IsReadOnly => ((ICollection<T>)_buffer).IsReadOnly;

    private T[] _buffer;
    private int _mask;

    private readonly static T[] EmptyArray = [];
    private const int MINIMUM_POWER_GROW = 1;
    private const int GROW_FACTOR = 200;

    public IndexedQueue(int capacity) : this(capacity, false) 
    {

    }
    public IndexedQueue(int initialCapacity = 4, bool growable = true)
    {
        if ((initialCapacity & (initialCapacity - 1)) != 0)
        {
            throw new ArgumentException("Capacity must be a power of two");
        }
        this.Capacity = initialCapacity;
        this.InitialCapacity = initialCapacity;
        this._mask = initialCapacity - 1;
        this._buffer = new T[initialCapacity];
        this.Growable = growable;
        Start = 0;
        Count = 0;
    }

    public T this[int index]
    {
        get
        {
            return index < 0 || index >= Count ? throw new ArgumentOutOfRangeException() : _buffer[(Start + index) & _mask];
        }
    }

	public void PushFront(T item)
	{
		Start = (Start - 1) & _mask;
		_buffer[Start] = item;
		Count++;
	}
	public T PopFront()
	{
		if (Count == 0) throw new InvalidOperationException();
		T item = _buffer[Start];
		_buffer[Start] = default!;
		Start = (Start + 1) & _mask;
		Count--;
		return item;
	}


	public void Enqueue(T item)
    {
        if (Count == Capacity)
        {
            if (Growable)
            {
                Grow();
            } 
            else
            {
                // Overwrite oldest item
                Start = (Start + 1) & _mask;
                Count--;
            }
        }

        int index = (Start + Count) & _mask;
        _buffer[index] = item;
        Count++;
    }
    public void TryShrink(int shrinkFactor = 1)
    {
        int shrinkThreshold = Capacity >> shrinkFactor;
        if (Count >= shrinkThreshold || Capacity <= InitialCapacity)
        {
            return;
        }
        int newCapacity = NextPowerOfTwo(Count);
        if (newCapacity < InitialCapacity)
            newCapacity = InitialCapacity;

        T[] newBuffer = new T[newCapacity];
        for (int i = 0; i < Count; i++)
        {
            newBuffer[i] = _buffer[(Start + i) & _mask];
        }

        _buffer = newBuffer;
        Capacity = newCapacity;
        _mask = newCapacity - 1;
        Start = 0;
    }
    private void Grow()
    {
        int newCapacity = Capacity << 1;
        if (MaxCapacity > 0 && MaxCapacity < newCapacity)
        {
            newCapacity = MaxCapacity;
        }

        if (newCapacity == Capacity) throw new InvalidOperationException("Queue has reached its maximum capacity");

        T[] newBuffer = new T[newCapacity];
        for (int i = 0; i < Count; i++)
        {
            newBuffer[i] = _buffer[(Start + i) & _mask];
        }
        _buffer = newBuffer;
        Capacity = newCapacity;
        _mask = newCapacity - 1;
        Start = 0;
    }
    private static int NextPowerOfTwo(int value)
    {
        if (value < 1) return 1;
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }
    public T Dequeue()
    {
        if (Count == 0)
            throw new InvalidOperationException("Queue is empty");

        T item = _buffer[Start];
        Start = (Start + 1) & _mask;
        Count--;
        
		TryShrink();
        return item;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
        {
            yield return _buffer[(Start + i) & _mask];
        }
    }
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    public object Clone() => CloneThis();
    public IndexedQueue<T> CloneThis()
    {
        var clone = new IndexedQueue<T>(Capacity);
        for (int i = 0; i < Count; i++)
        {
            clone.Enqueue(this[i]);
        }
        return clone;
    }
    public void Clear()
    {
        Array.Clear(_buffer, 0, _buffer.Length);
        Start = 0;
        Count = 0;
    }
    public bool Contains(T item)
    {
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < Count; i++)
        {
            if (comparer.Equals(_buffer[(Start + i) & _mask], item))
                return true;
        }
        return false;
    }
    public void CopyTo(T[] array, int arrayIndex)
    {
        if (array == null)
            throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < Count)
            throw new ArgumentException("Not enough space in the target array.");

        for (int i = 0; i < Count; i++)
        {
            array[arrayIndex + i] = _buffer[(Start + i) & _mask];
        }
    }
    public void Add(T item)
    {
        ((ICollection<T>)_buffer).Add(item);
    }
    public bool Remove(T item)
    {
        return ((ICollection<T>)_buffer).Remove(item);
    }
}