using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using System.Text;

namespace HackPDM.Abstractions
{
	/// <summary>
	/// A two-way dictionary: every key maps to one value and every value back to its key, so lookups work in both
	/// directions through <see cref="Forward"/> and <see cref="Backward"/>.
	/// </summary>
	/// <typeparam name="TKey">The key type; the values of <see cref="Backward"/>.</typeparam>
	/// <typeparam name="TValue">The value type; it must be unique too, as the keys of <see cref="Backward"/>.</typeparam>
	/// <remarks>
	/// The overloads that take a <typeparamref name="TValue"/> as key become ambiguous when both types are the same.
	/// Not thread-safe.
	/// </remarks>
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
		public int Count => Forward.Count;
		public bool IsReadOnly => false;

		public ICollection<TKey> Keys => ((IDictionary<TKey, TValue>)Forward).Keys;
		public ICollection<TValue> Values => ((IDictionary<TKey, TValue>)Forward).Values;
		ICollection IDictionary.Keys => ((IDictionary)Forward).Keys;
		ICollection IDictionary.Values => ((IDictionary)Forward).Values;

		IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => ((IReadOnlyDictionary<TKey, TValue>)Forward).Keys;
		IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => ((IReadOnlyDictionary<TKey, TValue>)Forward).Values;
		public bool IsSynchronized => ((ICollection)Forward).IsSynchronized;
		public object SyncRoot => ((ICollection)Forward).SyncRoot;
		public bool IsFixedSize => ((IDictionary)Forward).IsFixedSize;

		/// <summary>Gets or sets the key mapped to a value.</summary>
		/// <param name="key">The value to look up.</param>
		/// <value>The key. Setting it only updates <see cref="Backward"/>, leaving <see cref="Forward"/> out of sync.</value>
		/// <exception cref="System.Collections.Generic.KeyNotFoundException">Getting a value that isn't mapped.</exception>
		public TKey this[TValue key] { get => ((IDictionary<TValue, TKey>)Backward)[key]; set => ((IDictionary<TValue, TKey>)Backward)[key] = value; }
		public object this[object key] { get => ((IDictionary)Forward)[key]; set => ((IDictionary)Forward)[key] = value; }
		/// <summary>Gets or sets the value mapped to a key.</summary>
		/// <param name="key">The key to look up.</param>
		/// <value>The value. Setting it only updates <see cref="Forward"/>, leaving <see cref="Backward"/> out of sync.</value>
		/// <exception cref="System.Collections.Generic.KeyNotFoundException">Getting a key that isn't mapped.</exception>
		public TValue this[TKey key] { get => ((IDictionary<TKey, TValue>)Forward)[key]; set => ((IDictionary<TKey, TValue>)Forward)[key] = value; }

		/// <summary>Creates an empty map.</summary>
		public BiMap()
		{
			Forward = [];
			Backward = [];
		}
		/// <summary>Creates an empty map with room for a number of pairs.</summary>
		/// <param name="capacity">The initial capacity of both directions.</param>
		public BiMap(int capacity)
		{
			Forward = new Dictionary<TKey, TValue>(capacity);
			Backward = new Dictionary<TValue, TKey>(capacity);
		}
		/// <summary>Creates an empty map with custom equality comparers.</summary>
		/// <param name="keyComparer">Compares keys.</param>
		/// <param name="valueComparer">Compares values.</param>
		public BiMap(IEqualityComparer<TKey> keyComparer, IEqualityComparer<TValue> valueComparer)
		{
			Forward = new Dictionary<TKey, TValue>(keyComparer);
			Backward = new Dictionary<TValue, TKey>(valueComparer);
		}
		/// <summary>Creates an empty map with an initial capacity and custom equality comparers.</summary>
		/// <param name="capacity">The initial capacity of both directions.</param>
		/// <param name="keyComparer">Compares keys.</param>
		/// <param name="valueComparer">Compares values.</param>
		public BiMap(int capacity, IEqualityComparer<TKey> keyComparer, IEqualityComparer<TValue> valueComparer)
		{
			Forward = new Dictionary<TKey, TValue>(capacity, keyComparer);
			Backward = new Dictionary<TValue, TKey>(capacity, valueComparer);
		}
		/// <summary>Creates a map holding the pairs of a dictionary.</summary>
		/// <param name="dictionary">The pairs to copy.</param>
		/// <exception cref="System.ArgumentException">Two keys of <paramref name="dictionary"/> have the same value.</exception>
		public BiMap(IDictionary<TKey, TValue> dictionary)
		{
			Forward = new Dictionary<TKey, TValue>(dictionary);
			Backward = [];
			foreach (var kvp in dictionary)
			{
				Backward.Add(kvp.Value, kvp.Key);
			}
		}
		

		/// <summary>Adds a pair given in key-to-value order.</summary>
		/// <param name="item">The key and its value.</param>
		/// <exception cref="System.ArgumentException">The key or the value is already mapped (the forward side may already have been added).</exception>
		public void AddFront(KeyValuePair<TKey, TValue> item)
		{
			Forward.Add(item.Key, item.Value);
			Backward.Add(item.Value, item.Key);
		}
		/// <summary>Adds a pair given in value-to-key order.</summary>
		/// <param name="item">The value and its key.</param>
		/// <exception cref="System.ArgumentException">The key or the value is already mapped (the backward side may already have been added).</exception>
		public void AddBack(KeyValuePair<TValue, TKey> item)
		{
			Backward.Add(item.Key, item.Value);
			Forward.Add(item.Value, item.Key);
		}
		
		/// <summary>Removes every pair from both directions.</summary>
		public void Clear()
		{
			Forward.Clear();
			Backward.Clear();
		}
		

		/// <summary>Adds a pair given in key-to-value order.</summary>
		/// <param name="item">The key and its value.</param>
		/// <exception cref="System.ArgumentException">The key or the value is already mapped.</exception>
		public void Add(KeyValuePair<TKey, TValue> item)
		{
			((ICollection<KeyValuePair<TKey, TValue>>)Forward).Add(item);
			((ICollection<KeyValuePair<TValue, TKey>>)Backward).Add(new KeyValuePair<TValue, TKey>(item.Value, item.Key));
		}
		/// <summary>Adds a key and its value.</summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		/// <exception cref="System.ArgumentException">The key or the value is already mapped.</exception>
		public void Add(TKey key, TValue value)
		{
			((IDictionary<TKey, TValue>)Forward).Add(key, value);
			((IDictionary<TValue, TKey>)Backward).Add(value, key);
		}
		/// <summary>Adds a pair given value first.</summary>
		/// <param name="key">The value (the key of <see cref="Backward"/>).</param>
		/// <param name="value">Its key.</param>
		/// <exception cref="System.ArgumentException">The key or the value is already mapped.</exception>
		public void Add(TValue key, TKey value)
		{
			((IDictionary<TValue, TKey>)Backward).Add(key, value);
			((IDictionary<TKey, TValue>)Forward).Add(value, key);
		}
		/// <summary>Adds a pair through the non-generic dictionary interface.</summary>
		/// <param name="key">The key, which must be a <typeparamref name="TKey"/>.</param>
		/// <param name="value">The value, which must be a <typeparamref name="TValue"/>.</param>
		/// <exception cref="System.ArgumentException">The key or the value is already mapped, or <paramref name="key"/> has the wrong type.</exception>
		/// <exception cref="System.InvalidCastException"><paramref name="value"/> or <paramref name="key"/> has the wrong type.</exception>
		public void Add(object key, object value)
		{
			((IDictionary)Forward).Add(key, value);
			Backward.Add((TValue)value, (TKey)key);
		}
		/// <summary>Adds a pair given in value-to-key order.</summary>
		/// <param name="item">The value and its key.</param>
		/// <exception cref="System.ArgumentException">The key or the value is already mapped.</exception>
		public void Add(KeyValuePair<TValue, TKey> item)
		{
			((ICollection<KeyValuePair<TValue, TKey>>)Backward).Add(item);
			((ICollection<KeyValuePair<TKey, TValue>>)Forward).Add(new KeyValuePair<TKey, TValue>(item.Value, item.Key));
		}

		/// <summary>Looks up the value of a key.</summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value when found; otherwise the default.</param>
		/// <returns><see langword="true"/> when the key is mapped.</returns>
		public bool TryGetValue(TKey key, out TValue value)
		{
			return ((IDictionary<TKey, TValue>)Forward).TryGetValue(key, out value);
		}
		/// <summary>Looks up the key of a value.</summary>
		/// <param name="key">The value.</param>
		/// <param name="value">The key when found; otherwise the default.</param>
		/// <returns><see langword="true"/> when the value is mapped.</returns>
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
		/// <summary>Tells whether a value is mapped.</summary>
		/// <param name="value">The value.</param>
		/// <returns><see langword="true"/> when some key maps to it.</returns>
		public bool ContainsValue(object value) => ((IDictionary)Backward).Contains(value);

		/// <summary>Tells whether a key is mapped.</summary>
		/// <param name="key">The key.</param>
		/// <returns><see langword="true"/> when the key is mapped.</returns>
		public bool ContainsKey(TKey key)
		{
			return ((IDictionary<TKey, TValue>)Forward).ContainsKey(key);
		}
		/// <summary>Tells whether a value is mapped.</summary>
		/// <param name="key">The value.</param>
		/// <returns><see langword="true"/> when some key maps to it.</returns>
		public bool ContainsKey(TValue key)
			=> ((IDictionary<TValue, TKey>)Backward).ContainsKey(key);

		/// <summary>Removes a key and its value from both directions.</summary>
		/// <param name="key">The key.</param>
		/// <returns><see langword="true"/> when both sides had an entry to remove.</returns>
		/// <remarks>Finds the backward entry by a linear scan.</remarks>
		public bool Remove(TKey key)
			=> ((IDictionary<TValue, TKey>)Backward).Remove(Backward.FirstOrDefault(valCollect => key.Equals(valCollect.Value)).Key) & ((IDictionary<TKey, TValue>)Forward).Remove(key);
		/// <summary>Removes the pair's key from the forward side and its value from the backward side.</summary>
		/// <param name="item">The pair; the two sides are removed independently, without checking they belong together.</param>
		/// <returns><see langword="true"/> when both removals succeeded.</returns>
		public bool Remove(KeyValuePair<TKey, TValue> item)
			=> Forward.Remove(item.Key) & Backward.Remove(item.Value);
		/// <summary>Removes a key through the non-generic dictionary interface.</summary>
		/// <param name="key">The key.</param>
		/// <remarks>
		/// The backward entry removed is not the one belonging to <paramref name="key"/>: the scan's condition ignores the
		/// entry it tests, so the first backward entry is removed whenever <paramref name="key"/>, read as a value, is mapped.
		/// </remarks>
		/// <exception cref="System.InvalidCastException"><paramref name="key"/> is not a <typeparamref name="TValue"/>.</exception>
		public void Remove(object key)
		{
			((IDictionary)Backward).Remove(Backward.FirstOrDefault(valCollect => TryGetValue((TValue)key, out _)).Key);
			((IDictionary)Forward).Remove(key);
		}
		/// <summary>Removes a pair given in value-to-key order.</summary>
		/// <param name="item">The value and its key.</param>
		/// <returns><see langword="true"/> when both removals succeeded.</returns>
		/// <remarks>The forward entry removed is the first one in the map, not necessarily the pair's (the scan's condition is always true).</remarks>
		public bool Remove(KeyValuePair<TValue, TKey> item)
			=> Forward.Remove(Forward.FirstOrDefault(keyCollect => item.Key.Equals(item.Key)).Key) & ((ICollection<KeyValuePair<TValue, TKey>>)Backward).Remove(item);
		/// <summary>Meant to remove a value and its key from both directions.</summary>
		/// <param name="key">The value.</param>
		/// <returns>Does not return normally unless <typeparamref name="TKey"/> and <typeparamref name="TValue"/> are the same type.</returns>
		/// <exception cref="System.InvalidCastException">Always when the two types differ: <see cref="Forward"/> is cast to the reversed dictionary type.</exception>
		public bool Remove(TValue key)
			=> ((IDictionary<TValue, TKey>)Forward).Remove(Forward.FirstOrDefault(valCollect => key.Equals(valCollect.Value)).Value) & ((IDictionary<TValue, TKey>)Backward).Remove(key);

		public void OnDeserialization(object sender)
			=> ((IDeserializationCallback)Forward).OnDeserialization(sender);
		public void GetObjectData(SerializationInfo info, StreamingContext context)
			=> ((ISerializable)Forward).GetObjectData(info, context);
		
		

		/// <summary>Enumerates the pairs in value-to-key order.</summary>
		/// <returns>An enumerator over <see cref="Backward"/>.</returns>
		public IEnumerator<KeyValuePair<TValue, TKey>> GetBackEnumerator()
			=> Backward.GetEnumerator();
		IDictionaryEnumerator BackEnumerator => ((IDictionary)Backward).GetEnumerator();
		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Forward.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		IDictionaryEnumerator IDictionary.GetEnumerator()
			=> ((IDictionary)Forward).GetEnumerator();
	}

}
