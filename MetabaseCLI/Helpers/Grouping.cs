using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MetabaseCLI
{
    internal class Grouping<TKey, TValue> :
    IGrouping<TKey, TValue>
    where TKey : notnull
    {
        public Grouping(TKey key, IEnumerable<TValue> value)
        {
            Key = key;
            Value = value;
        }

        public TKey Key { get; private set; }
        public IEnumerable<TValue> Value { get; private set; }

        public IEnumerator<TValue> GetEnumerator() => Value.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}