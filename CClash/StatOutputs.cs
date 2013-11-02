using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public class StatOutputs
    {
        public static string GetStatsString(string compiler)
        {
            var sb = new StringBuilder();
            sb.WriteLine("compiler: {0}", compiler);
            sb.WriteLine("cachedir: {0}", Settings.CacheDirectory);
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
                using (var cache = new DirectCompilerCache(Settings.CacheDirectory))
                {
                    sb.WriteLine("outputCache usage: {0} kb", (int)(cache.Stats.CacheSize / 1024));
                    sb.WriteLine("cached files: {0}", cache.Stats.CacheObjects);
                    sb.WriteLine("hits: {0}", cache.Stats.CacheHits);
                    sb.WriteLine("misses: {0}", cache.Stats.CacheMisses);
                    sb.WriteLine("unsupported: {0}", cache.Stats.CacheUnsupported);
                    sb.WriteLine("slow hits: {0}", cache.Stats.SlowHitCount);
                    sb.WriteLine("time lost: {0} mins", Math.Round(cache.Stats.MSecLost / 60000.0));
                }
            }
            return sb.ToString();
        }
    }
}
