using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using System.Text;

namespace HackPDM.Abstractions
{
	public class BiMap<TKey, TValue> :
		ICollection<KeyValuePair<TKey, TValue>>,
		IEnumerable<KeyValuePair<TKey, TValue>>,
		IEnumerable, IDictionary<TKey, TValue>,
		IReadOnlyCollection<KeyValuePair<TKey, TValue>>,
		IReadOnlyDictionary<TKey, TValue>,
		ICollection,
		IDictionary,
		IDeserializationCallback,
		ISerializable
	{
		public Dictionary<TKey, TValue> Forward { get; }
		public Dictionary<TValue, TKey> Backward { get; }
		public int Count { get; }
		public bool IsReadOnly { get; }

		public ICollection<TKey> Keys => ((IDictionary<TKey, TValue>)Forward).Keys;
		public ICollection<TValue> Values => ((IDictionary<TKey, TValue>)Forward).Values;
		ICollection IDictionary.Keys => ((IDictionary)Forward).Keys;
		ICollection IDictionary.Values => ((IDictionary)Forward).Values;

		IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => ((IReadOnlyDictionary<TKey, TValue>)Forward).Keys;
		IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => ((IReadOnlyDictionary<TKey, TValue>)Forward).Values;
		public bool IsSynchronized => ((ICollection)Forward).IsSynchronized;
		public object SyncRoot => ((ICollection)Forward).SyncRoot;
		public bool IsFixedSize => ((IDictionary)Forward).IsFixedSize;

		public TKey this[TValue key] { get => ((IDictionary<TValue, TKey>)Backward)[key]; set => ((IDictionary<TValue, TKey>)Backward)[key] = value; }
		public object this[object key] { get => ((IDictionary)Forward)[key]; set => ((IDictionary)Forward)[key] = value; }
		public TValue this[TKey key] { get => ((IDictionary<TKey, TValue>)Forward)[key]; set => ((IDictionary<TKey, TValue>)Forward)[key] = value; }

		public BiMap()
		{
			Forward = [];
			Backward = [];
		}
		public BiMap(int capacity)
		{
			Forward = new Dictionary<TKey, TValue>(capacity);
			Backward = new Dictionary<TValue, TKey>(capacity);
		}
		public BiMap(IEqualityComparer<TKey> keyComparer, IEqualityComparer<TValue> valueComparer)
		{
			Forward = new Dictionary<TKey, TValue>(keyComparer);
			Backward = new Dictionary<TValue, TKey>(valueComparer);
		}
		public BiMap(int capacity, IEqualityComparer<TKey> keyComparer, IEqualityComparer<TValue> valueComparer)
		{
			Forward = new Dictionary<TKey, TValue>(capacity, keyComparer);
			Backward = new Dictionary<TValue, TKey>(capacity, valueComparer);
		}
		public BiMap(IDictionary<TKey, TValue> dictionary)
		{
			Forward = new Dictionary<TKey, TValue>(dictionary);
			Backward = [];
			foreach (var kvp in dictionary)
			{
				Backward.Add(kvp.Value, kvp.Key);
			}
		}
		

		public void AddFront(KeyValuePair<TKey, TValue> item)
		{
			Forward.Add(item.Key, item.Value);
			Backward.Add(item.Value, item.Key);
		}
		public void AddBack(KeyValuePair<TValue, TKey> item)
		{
			Backward.Add(item.Key, item.Value);
			Forward.Add(item.Value, item.Key);
		}
		
		public void Clear()
		{
			Forward.Clear();
			Backward.Clear();
		}
		

		public void Add(KeyValuePair<TKey, TValue> item)
		{
			((ICollection<KeyValuePair<TKey, TValue>>)Forward).Add(item);
			((ICollection<KeyValuePair<TValue, TKey>>)Backward).Add(new KeyValuePair<TValue, TKey>(item.Value, item.Key));
		}
		public void Add(TKey key, TValue value)
		{
			((IDictionary<TKey, TValue>)Forward).Add(key, value);
			((IDictionary<TValue, TKey>)Backward).Add(value, key);
		}
		public void Add(TValue key, TKey value)
		{
			((IDictionary<TValue, TKey>)Backward).Add(key, value);
			((IDictionary<TKey, TValue>)Forward).Add(value, key);
		}
		public void Add(object key, object value)
		{
			((IDictionary)Forward).Add(key, value);
			Backward.Add((TValue)value, (TKey)key);
		}
		public void Add(KeyValuePair<TValue, TKey> item)
		{
			((ICollection<KeyValuePair<TValue, TKey>>)Backward).Add(item);
			((ICollection<KeyValuePair<TKey, TValue>>)Forward).Add(new KeyValuePair<TKey, TValue>(item.Value, item.Key));
		}

		public bool TryGetValue(TKey key, out TValue value)
		{
			return ((IDictionary<TKey, TValue>)Forward).TryGetValue(key, out value);
		}
		public bool TryGetValue(TValue key, out TKey value)
			=> ((IDictionary<TValue, TKey>)Backward).TryGetValue(key, out value);

		public void CopyTo(Array array, int index)
			=> ((ICollection)Forward).CopyTo(array, index);
		public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
			=> ((ICollection<KeyValuePair<TKey, TValue>>)Forward).CopyTo(array, arrayIndex);
		public void CopyTo(KeyValuePair<TValue, TKey>[] array, int arrayIndex)
			=> ((ICollection<KeyValuePair<TValue, TKey>>)Backward).CopyTo(array, arrayIndex);

		public bool Contains(object key)
			=> ((IDictionary)Forward).Contains(key);
		public bool Contains(KeyValuePair<TKey, TValue> item)
			=> Forward.Contains(item);
		public bool Contains(KeyValuePair<TValue, TKey> item)
			=> ((ICollection<KeyValuePair<TValue, TKey>>)Backward).Contains(item);
		public bool ContainsValue(object value) => ((IDictionary)Backward).Contains(value);

		public bool ContainsKey(TKey key)
		{
			return ((IDictionary<TKey, TValue>)Forward).ContainsKey(key);
		}
		public bool ContainsKey(TValue key)
			=> ((IDictionary<TValue, TKey>)Backward).ContainsKey(key);

		public bool Remove(TKey key)
			=> ((IDictionary<TValue, TKey>)Backward).Remove(Backward.FirstOrDefault(valCollect => key.Equals(valCollect.Value)).Key) & ((IDictionary<TKey, TValue>)Forward).Remove(key);
		public bool Remove(KeyValuePair<TKey, TValue> item)
			=> Forward.Remove(item.Key) & Backward.Remove(item.Value);
		public void Remove(object key)
		{
			((IDictionary)Backward).Remove(Backward.FirstOrDefault(valCollect => TryGetValue((TValue)key, out _)).Key);
			((IDictionary)Forward).Remove(key);
		}
		public bool Remove(KeyValuePair<TValue, TKey> item)
			=> Forward.Remove(Forward.FirstOrDefault(keyCollect => item.Key.Equals(item.Key)).Key) & ((ICollection<KeyValuePair<TValue, TKey>>)Backward).Remove(item);
		public bool Remove(TValue key)
			=> ((IDictionary<TValue, TKey>)Forward).Remove(Forward.FirstOrDefault(valCollect => key.Equals(valCollect.Value)).Value) & ((IDictionary<TValue, TKey>)Backward).Remove(key);

		public void OnDeserialization(object sender)
			=> ((IDeserializationCallback)Forward).OnDeserialization(sender);
		public void GetObjectData(SerializationInfo info, StreamingContext context)
			=> ((ISerializable)Forward).GetObjectData(info, context);
		
		

		public IEnumerator<KeyValuePair<TValue, TKey>> GetBackEnumerator()
			=> Backward.GetEnumerator();
		IDictionaryEnumerator BackEnumerator => ((IDictionary)Backward).GetEnumerator();
		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Forward.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		IDictionaryEnumerator IDictionary.GetEnumerator()
			=> ((IDictionary)Forward).GetEnumerator();
	}

}
