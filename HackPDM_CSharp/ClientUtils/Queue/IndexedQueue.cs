using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HackPDM.ClientUtils.Queue
{
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
        public object SyncRoot => buffer.SyncRoot;
        public bool IsSynchronized => buffer.IsSynchronized;
        public bool IsReadOnly => ((ICollection<T>)buffer).IsReadOnly;

        private T[] buffer;
        private int mask;

        private readonly static T[] _emptyArray = [];
        private const int _MinimumPowerGrow = 1;
        private const int _GrowFactor = 200;

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
            this.mask = initialCapacity - 1;
            this.buffer = new T[initialCapacity];
            this.Growable = growable;
            Start = 0;
            Count = 0;
        }

        public T this[int index]
        {
            get
            {
                return index < 0 || index >= Count ? throw new ArgumentOutOfRangeException() : buffer[(Start + index) & mask];
            }
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
                    Start = (Start + 1) & mask;
                    Count--;
                }
            }

            int index = (Start + Count) & mask;
            buffer[index] = item;
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
                newBuffer[i] = buffer[(Start + i) & mask];
            }

            buffer = newBuffer;
            Capacity = newCapacity;
            mask = newCapacity - 1;
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
                newBuffer[i] = buffer[(Start + i) & mask];
            }
            buffer = newBuffer;
            Capacity = newCapacity;
            mask = newCapacity - 1;
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

            T item = buffer[Start];
            Start = (Start + 1) & mask;
            Count--;
            //TryShrink();
            return item;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return buffer[(Start + i) & mask];
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
            Array.Clear(buffer, 0, buffer.Length);
            Start = 0;
            Count = 0;
        }
        public bool Contains(T item)
        {
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < Count; i++)
            {
                if (comparer.Equals(buffer[(Start + i) & mask], item))
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
                array[arrayIndex + i] = buffer[(Start + i) & mask];
            }
        }
        public void Add(T item)
        {
            ((ICollection<T>)buffer).Add(item);
        }
        public bool Remove(T item)
        {
            return ((ICollection<T>)buffer).Remove(item);
        }
    }
}
