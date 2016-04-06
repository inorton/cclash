using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CClash
{
    public sealed class FastCacheInfo : IDisposable, ICacheInfo
    {
        IFileCacheStore statstore;
        public FastCacheInfo(IFileCacheStore stats)
        {
            statstore = stats;
        }

        public long CacheHits
        {
            get;
            set;
        }

        public long CacheMisses
        {
            get;
            set;
        }

        public long CacheObjects
        {
            get;
            set;
        }

        public long CacheSize
        {
            get;
            set;
        }

        public long CacheUnsupported
        {
            get;
            set;
        }

        public void LockStatsCall(Action x)
        {
            x.Invoke();
        }

        public long SlowHitCount
        {
            get;
            set;
        }

        public void Commit()
        {
            using (var real = new CacheInfo(statstore))
            {
                real.LockStatsCall(() =>
                {
                    real.SlowHitCount += this.SlowHitCount;
                    real.CacheUnsupported += this.CacheUnsupported;
                    real.CacheSize += this.CacheSize;
                    real.CacheObjects += this.CacheObjects;
                    real.CacheMisses += this.CacheMisses;
                    real.CacheHits += this.CacheHits;
                });
            }
        }

        public void Dispose()
        {
            Commit();
        }
    }

    public class CacheInfo : IDisposable, ICacheInfo
    {

        public const string K_Stats = "stats";

        public const string F_StatObjects = "objects.txt";
        public const string F_StatDiskUsage = "usage.txt";
        public const string F_StatHits = "hits.txt";
        public const string F_StatMiss = "misses.txt";
        public const string F_StatUnsupported = "unsupported.txt";

        public const string F_StatSlowHits = "slow_hits.txt";
        public const string F_CacheVersion = "version.txt";

        public const string CacheFormat = "v3";

        IFileCacheStore cache;
        Mutex statMtx = null;

        public void Commit()
        {

        }

        public CacheInfo(IFileCacheStore statCache)
        {
            cache = statCache;
            Logging.Emit("creating cache info mutex");
            statMtx = new Mutex(false, "cclash_stat_" + cache.FolderPath.ToLower().GetHashCode());
            Logging.Emit("created cache info mutex");
        }


        public void LockStatsCall(Action x)
        {
            statMtx.WaitOne();
            x.Invoke();
            statMtx.ReleaseMutex(); 
        }

        public long ReadStat(string statfile)
        {
            try
            {
                cache.EnsureKey(K_Stats);
                using (var stats = cache.OpenFileStream(K_Stats, statfile, FileMode.Open, FileAccess.Read))
                {
                    using (var sr = new StreamReader(stats))
                    {
                        var x = sr.ReadToEnd();
                        return Int64.Parse(x);
                    }
                }                
            }
            catch
            {
                return 0;
            }
        }

        public void WriteStat(string statfile, long value)
        {
            cache.EnsureKey(K_Stats);
            using (var stats = cache.OpenFileStream(K_Stats, statfile, FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (var sw = new StreamWriter(stats))
                {
                    sw.Write(value.ToString());
                }
            }
        }

        public long SlowHitCount
        {
            get
            {
                return ReadStat(F_StatSlowHits);
            }
            set
            {
                WriteStat(F_StatSlowHits, value);
            }
        }

        public long CacheHits
        {
            get
            {
                return ReadStat(F_StatHits);
            }
            set
            {
                WriteStat(F_StatHits, value);
            }
        }

        public long CacheSize
        {
            get
            {
                return ReadStat(F_StatDiskUsage);
            }
            set
            {
                WriteStat(F_StatDiskUsage, value);
            }
        }

        public long CacheMisses
        {
            get
            {
                return ReadStat(F_StatMiss);
            }
            set
            {
                WriteStat(F_StatMiss, value);
            }
        }

        public long CacheUnsupported
        {
            get
            {
                return ReadStat(F_StatUnsupported);
            }
            set
            {
                WriteStat(F_StatUnsupported, value);
            }
        }

        public long CacheObjects
        {
            get
            {
                return ReadStat(F_StatObjects);
            }
            set
            {
                WriteStat(F_StatObjects, value);
            }
        }

        private void Dispose(bool disposing)
        {
            if ( disposing ) statMtx.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
        }
        
    }
}
