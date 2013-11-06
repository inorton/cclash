using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    sealed class CacheItem<T> {
        public DateTime timestamp;
        public T item;
    }

    public sealed class MemoryCache<T>
    {
        public MemoryCache()
        {
            MaxAgeSeconds = 10;
        }

        public int MaxAgeSeconds { get; set; }

        Dictionary<string, CacheItem<T>> data = new Dictionary<string, CacheItem<T>>();

        public bool TryGet( string key, out T value )
        {
            CacheItem<T> v;
            value = default(T);
            if (data.TryGetValue(key, out v))
            {
                if (DateTime.Now.Subtract(v.timestamp).TotalSeconds < MaxAgeSeconds)
                {
                    value = v.item;
                    return true;
                }
            }
                 
            return false;
        }

        public void Add(string key, T value)
        {
            data[key] = new CacheItem<T>() { timestamp = DateTime.Now, item = value };
        }

        public void Remove(string key)
        {
            if (data.ContainsKey(key))
                data.Remove(key);
        }
    }
}
