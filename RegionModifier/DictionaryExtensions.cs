using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegionModifier
{
    public static class DictionaryExtensions
    {
        public static V GetOrDefault<K,V>(this IDictionary<K,V> dict, K key) => dict.ContainsKey(key) ? dict[key] : default;
    }
}
