using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

namespace metastrings
{
    [JsonObject(MemberSerialization.OptIn)]
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

        [JsonProperty] 
        private List<KeyValuePair<K, V>> m_list { get; set; } = new List<KeyValuePair<K, V>>();
        [JsonProperty] 
        private Dictionary<K, V> m_dict { get; set; } = new Dictionary<K, V>();
    }
}
