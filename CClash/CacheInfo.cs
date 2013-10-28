using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public sealed class CacheInfo
    {
        public const string StatHits = "Hits";
        public const string StatMisses = "Misses";
        public const string StatSize = "Size";
        public const string StatUnsupported = "Unsupported";

        public Dictionary<string, int> Stats { get; set; }

        public const string ConfigMaxSize = "MaxSize";

        public Dictionary<string, int> Settings { get; set; }
    }
}
