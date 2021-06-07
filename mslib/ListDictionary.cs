using System;
using System.Collections.Generic;
using System.Linq;

namespace metastrings
{
    /// <summary>
    /// A hybrid class, Dictionary with a List used to track order added
    /// Add-only, read-only...makes for clean code!
    /// Use the Entries properties to iterate over the Key-Values in order added.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public class ListDictionary<K, V>
    {
        public ListDictionary()
        {
        }

        public ListDictionary(ListDictionary<K, V> dict)
        {
            foreach (var kvp in dict.Entries)
                Add(kvp.Key, kvp.Value);
        }

        public ListDictionary(Dictionary<K, V> dict)
        {
            foreach (var kvp in dict)
                Add(kvp.Key, kvp.Value);
        }

        public List<KeyValuePair<K, V>> Entries => m_list;
        public IEnumerable<K> Keys => m_list.Select(kvp => kvp.Key);
        public IEnumerable<V> Values => m_list.Select(kvp => kvp.Value);

        public V this[K key] => m_dict[key];

        public int Count => m_list.Count;

        public bool ContainsKey(K key) => m_dict.ContainsKey(key);

        public K FirstKey => m_list[0].Key;

        public void Add(K key, V val)
        {
            m_dict.Add(key, val);
            m_list.Add(new KeyValuePair<K, V>(key, val));
        }
        public bool TryGetValue(K key, out V val)
        {
            return m_dict.TryGetValue(key, out val);
        }

        private List<KeyValuePair<K, V>> m_list { get; set; } = new List<KeyValuePair<K, V>>();
        private Dictionary<K, V> m_dict { get; set; } = new Dictionary<K, V>();
    }
}
