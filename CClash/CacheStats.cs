using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CClash
{
    public sealed class CacheStats : IDisposable
    {

        public const string K_Stats = "stats";

        public const string F_StatObjects = "objects.txt";
        public const string F_StatDiskUsage = "usage.txt";
        public const string F_StatHits = "hits.txt";
        public const string F_StatMiss = "misses.txt";
        public const string F_StatUnsupported = "unsupported.txt";

        public const string F_StatSlowHits = "slow_hits.txt";
        public const string F_StatTimeWasted = "time_wasted.txt";

        FileCacheStore cache;
        Mutex statMtx = null;

        public bool OmitLocks
        {
            get;
            set;
        }

        public CacheStats(FileCacheStore statCache)
        {
            cache = statCache;
            statMtx = new Mutex(false, "cclash_stat_" + cache.FolderPath.ToLower().GetHashCode());
        }


        public void LockStatsCall(Action x)
        {
            if ( !OmitLocks ) statMtx.WaitOne();
            x.Invoke();
            if (!OmitLocks) statMtx.ReleaseMutex(); 
        }

        public long ReadStat(string statfile)
        {
            try
            {
                var x = System.IO.File.ReadAllText(cache.MakePath(K_Stats, statfile));
                return Int64.Parse(x);
            }
            catch
            {
                return 0;
            }
        }

        public void WriteStat(string statfile, long value)
        {
            cache.EnsureKey(K_Stats);
            System.IO.File.WriteAllText(cache.MakePath(K_Stats, statfile), value.ToString());
        }



        public long MSecLost
        {
            get
            {
                return ReadStat(F_StatTimeWasted);
            }
            set
            {
                WriteStat(F_StatTimeWasted, value);
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

        public void Dispose()
        {
            statMtx.Dispose();
        }
    }
}
