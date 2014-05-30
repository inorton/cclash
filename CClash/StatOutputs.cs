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
                    var stats = new CacheInfo(cache.OutputCache);

                    var mseclost = stats.MSecLost;
                    var msecsaved = stats.MSecSaved;

                    sb.WriteLine("outputCache usage: {0} kb", (int)(stats.CacheSize / 1024));
                    sb.WriteLine("cached files: {0}", stats.CacheObjects);
                    sb.WriteLine("hits: {0}", stats.CacheHits);
                    sb.WriteLine("misses: {0}", stats.CacheMisses);
                    sb.WriteLine("unsupported: {0}", stats.CacheUnsupported);
                    sb.WriteLine("slow hits: {0}", stats.SlowHitCount);
                    sb.WriteLine("cost of cache misses: {0:0.0} sec", mseclost / 1000.0 );
                    sb.WriteLine("cache hits saved: {0:0.0} sec", msecsaved / 1000.0);
                    sb.WriteLine("overall time saved: {0:0.0} mins", (msecsaved - mseclost) / 60000.0);
                }
            }
            return sb.ToString();
        }
    }
}
