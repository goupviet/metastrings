using System;
using System.Collections.Generic;
using System.Threading;

namespace metastrings
{
    public class SyncCache<K, V>
    {
        public bool TryGetValue(K key, out V value)
        {
            sm_lock.EnterReadLock();
            try
            {
                return m_dict.TryGetValue(key, out value);
            }
            finally
            {
                sm_lock.ExitReadLock();
            }
        }

        public void Put(K key, V value)
        {
            sm_lock.EnterWriteLock();
            try
            {
                m_dict[key] = value;
            }
            finally
            {
                sm_lock.ExitWriteLock();
            }
        }

        public void Put(IEnumerable<KeyValuePair<K, V>> entries)
        {
            sm_lock.EnterWriteLock();
            try
            {
                foreach (var kvp in entries)
                    m_dict[kvp.Key] = kvp.Value;
            }
            finally
            {
                sm_lock.ExitWriteLock();
            }
        }

        public void Clear()
        {
            sm_lock.EnterWriteLock();
            try
            {
                m_dict.Clear();
            }
            finally
            {
                sm_lock.ExitWriteLock();
            }
        }

        public V this[K key] { set { Put(key, value); } }

        public bool ContainsKey(K key)
        {
            sm_lock.EnterReadLock();
            try
            {
                return m_dict.ContainsKey(key);
            }
            finally
            {
                sm_lock.ExitReadLock();
            }
        }

        private Dictionary<K, V> m_dict = new Dictionary<K, V>();
        private static ReaderWriterLockSlim sm_lock = new ReaderWriterLockSlim();
    }
}
