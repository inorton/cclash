using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public class StatOutputs
    {
        public static string GetStatsString(string compiler, ICompilerCache cache)
        {
            var sb = new StringBuilder();
            sb.WriteLine("compiler: {0}", compiler);
            sb.WriteLine("cachedir: {0}", Settings.CacheDirectory);
            sb.WriteLine("cachetype: {0}", Settings.CacheType);
            if (Settings.DebugEnabled)
            {
                sb.WriteLine("debug file: {0}", Settings.DebugFile);
            }
            if (Settings.Disabled)
            {
                sb.WriteLine("disabled: yes");
            }
            else
            {
                sb.WriteLine("disabled: no");
            }
            if (compiler != null)
            {
                if (cache != null)
                {
                    using (var stats = new CacheInfo(cache.OutputCache))
                    {
                        sb.WriteLine("outputCache usage: {0} kb", (int)(stats.CacheSize / 1024));
                        sb.WriteLine("cached files: {0}", stats.CacheObjects);
                        sb.WriteLine("hits: {0}", stats.CacheHits);
                        sb.WriteLine("misses: {0}", stats.CacheMisses);
                        sb.WriteLine("unsupported: {0}", stats.CacheUnsupported);
                        sb.WriteLine("slow hits: {0}", stats.SlowHitCount);
                    }
                }
            }
            return sb.ToString();
        }
    }
}
