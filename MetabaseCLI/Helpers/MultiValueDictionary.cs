

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace MetabaseCLI
{
    public class MultiValueDictionary<TKey, TValue> :
    ILookup<TKey, TValue>
    where TKey : notnull
    {
        private readonly IDictionary<TKey, ICollection<TValue>> source;
        private readonly Func<ICollection<TValue>> collectionFactory;

        public MultiValueDictionary(
            IEqualityComparer<TKey> comparer,
            Func<ICollection<TValue>> collectionFactory
        )
        {
            source = new Dictionary<TKey, ICollection<TValue>>(comparer);
            this.collectionFactory = collectionFactory;
        }

        public MultiValueDictionary(
            IEqualityComparer<TKey> comparer
        ) : this(comparer, () => new List<TValue>())
        { }

        public MultiValueDictionary(
            Func<ICollection<TValue>> collectionFactory
        ) : this(EqualityComparer<TKey>.Default, collectionFactory)
        { }

        public MultiValueDictionary()
        : this(EqualityComparer<TKey>.Default) 
        { }
        
        public ICollection<TValue> this[TKey key] => source[key];

        public int Count => source.Count;

        public void Add(TKey key, TValue value)
        {
            if (!source.TryGetValue(key, out var collection))
            {
                collection = collectionFactory();
                source.Add(key, collection);
            }
            collection.Add(value);
        }

        public void Add(TKey key, IEnumerable<TValue> collection)
        {
            if (!source.TryGetValue(key, out var col))
            {
                col = collectionFactory();
                source.Add(key, col);
            }
            foreach (var item in collection)
            {
                col.Add(item);
            }
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out IEnumerable<TValue> collection)
        {
            if (source.TryGetValue(key, out var result))
            {    
                collection = result;
                return true;
            }
            else
            {
                collection = null;
                return false;
            }
        }

        public bool Remove(TKey key, TValue value) =>
            source.TryGetValue(key, out var collection) && collection.Remove(value);

        public bool Remove(TKey key) =>
            source.Remove(key);
        
        public IEnumerable<bool> Remove(TKey key, IEnumerable<TValue> collection) =>
            source.TryGetValue(key, out var col) ?
            collection.Select(item => col.Remove(item)).ToList() :
            collection.Select(item => false);

        IEnumerable<TValue> ILookup<TKey, TValue>.this[TKey key] => this[key];

        public bool ContainsKey(TKey key) => source.ContainsKey(key);

        public IEnumerable<ICollection<TValue>> Values
        {
            get => source.Values;
        }

        public IEnumerator<IGrouping<TKey, TValue>> GetEnumerator() =>
            source.Select(kv => new Grouping<TKey, TValue>(kv.Key, kv.Value)).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        bool ILookup<TKey, TValue>.Contains(TKey key) => this.ContainsKey(key);
    }
}