using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Web.Script.Serialization;
using System.Diagnostics;
using System.Threading;

namespace CClash
{
    public sealed class CompilerCache : IDisposable
    {
        static DateTime cacheStart = DateTime.Now;

        JavaScriptSerializer jss = new JavaScriptSerializer();
        FileCacheStore outputCache;
        FileCacheStore includeCache;
        String compilerPath;
        HashUtil hasher;
        Compiler comp;

        const string F_Manifest = "manifest.json";
        const string F_Object = "target.object";
        const string F_Pdb = "target.pdb";
        const string F_Stdout = "compiler.stdout";
        const string F_Stderr = "compiler.stderr";

        const string K_Stats = "stats";

        const string F_StatObjects = "objects.txt";
        const string F_StatDiskUsage = "usage.txt";
        const string F_StatHits = "hits.txt";
        const string F_StatMiss = "misses.txt";
        const string F_StatUnsupported = "unsupported.txt";

        const string F_StatSlowHits = "slow_hits.txt";
        const string F_StatTimeWasted = "time_wasted.txt";
        const string F_StatTimeSaved = "time_saved.txt";
        
        Mutex statMtx = null;

        void LockStatsCall(Action x)
        {
            if (statMtx == null)
            {
                statMtx = new Mutex(false, "cclash_stat_" + outputCache.FolderPath.ToLower().GetHashCode());
            }
            statMtx.WaitOne();
            x.Invoke();
            statMtx.ReleaseMutex();
        }

        long ReadStat(string statfile)
        {
            try
            {
                var x = File.ReadAllText(outputCache.MakePath(K_Stats, statfile));
                return Int64.Parse(x);
            }
            catch
            {
                return 0;
            }
        }

        void WriteStat(string statfile, long value)
        {
            outputCache.AddEntry(K_Stats);
            File.WriteAllText( outputCache.MakePath(K_Stats, statfile), value.ToString());
        }

        public long MSecLost
        {
            get
            {
                return ReadStat(F_StatTimeWasted);
            }
            private set
            {
                WriteStat(F_StatTimeWasted, value);
            }
        }

        public long MSecSaved
        {
            get
            {
                return ReadStat(F_StatTimeSaved);
            }
            private set
            {
                WriteStat(F_StatTimeSaved, value);
            }
        }

        public long SlowHitCount
        {
            get
            {
                return ReadStat(F_StatSlowHits);
            }
            private set
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
            private set
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
            private set
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
            private set
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
            private set
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
            private set
            {
                WriteStat(F_StatObjects, value);
            }
        }

        public CompilerCache(string cacheFolder, string compiler)
        {
            if (string.IsNullOrEmpty(cacheFolder)) throw new ArgumentNullException("cacheFolder");
            if (string.IsNullOrEmpty(compiler)) throw new ArgumentNullException("compiler");
            outputCache = FileCacheStore.Load(Path.Combine(cacheFolder, "outputs"));
            includeCache = FileCacheStore.Load(Path.Combine(cacheFolder, "includes"));

            hasher = new HashUtil(includeCache);
            compilerPath = System.IO.Path.GetFullPath(compiler);
            comp = new Compiler()
            {
                CompilerExe = compilerPath
            };
        }

        public bool IsSupported(IEnumerable<string> args)
        {
            if (FileUtils.Exists(compilerPath))
            {
                return comp.ProcessArguments(args.ToArray());
            }
            throw new FileNotFoundException(compilerPath);
        }

        public DataHash DeriveHashKey( IEnumerable<string> args )
        {
            var comphash = hasher.DigestBinaryFile(compilerPath);
            if (comphash.Result == DataHashResult.Ok)
            {
                var buf = new StringBuilder();
                var incs = Environment.GetEnvironmentVariable("INCLUDE");
                if (incs != null)
                    buf.AppendLine(incs);
                foreach (var a in args)
                    buf.AppendLine(a);
                buf.AppendLine(comphash.Hash);
                comphash = hasher.DigestString(buf.ToString());
            }
            return comphash;
        }

        /// <summary>
        /// When this returns, we will hold the output cache mutex.
        /// </summary>
        /// <param name="commonkey"></param>
        /// <param name="manifest"></param>
        /// <returns></returns>
        public bool CheckCache(DataHash commonkey, out CacheManifest manifest )
        {
            manifest = null;
            outputCache.WaitOne();
            if (outputCache.ContainsEntry(commonkey.Hash, F_Manifest))
            {
                var mn = outputCache.MakePath(commonkey.Hash, F_Manifest);
                
                var m = jss.Deserialize<CacheManifest>(File.ReadAllText(mn));
                manifest = m;
                if (m.Disable)
                {
                    Logging.Emit("disabled by manifest");
                    return false;
                }
                foreach ( var f in m.PotentialNewIncludes ) {
                    if (FileUtils.Exists(f))
                    {
                        Logging.Emit("detected added include file {0}", f);
                        return false;
                    }
                }
                var hashes = hasher.DigestFiles(m.IncludeFiles.Keys);

                foreach (var h in hashes)
                {
                    if (h.Value.Result == DataHashResult.Ok)
                    {

                        if (m.IncludeFiles[h.Key] != h.Value.Hash)
                        {
                            Logging.Emit("include file hash changed {0}", h.Key);
                            return false;
                        }
                    }
                    else
                    {
                        Logging.Emit("include file hash error {0} {1}", h.Key, h.Value.Result);
                        return false;
                    }
                }

                foreach (var f in new string[] { F_Manifest, F_Object, F_Stderr, F_Stdout })
                {
                    if (!FileUtils.Exists(outputCache.MakePath(commonkey.Hash, f)))
                    {
                        outputCache.Remove(commonkey.Hash);
                        return false;
                    }
                }

                return true; // cache hit, all includes match and no new files added
            }
            return false;
        }

        public int DoCacheHit(DataHash hc, CacheManifest hm)
        {
            // we dont need the lock now, it is highly unlikley someone else will
            // modify these files
            outputCache.ReleaseMutex();

            var stderrfile = outputCache.MakePath(hc.Hash, F_Stderr);
            var stdoutfile = outputCache.MakePath(hc.Hash, F_Stdout);

            LockStatsCall( () => CacheHits++ );

            // cache hit
            Console.Out.Write(File.ReadAllText(stdoutfile));
            Console.Error.Write(File.ReadAllText(stderrfile));
            File.Copy(outputCache.MakePath(hc.Hash, F_Object), comp.ObjectTarget, true);
            if (comp.GeneratePdb)
                File.Copy(outputCache.MakePath(hc.Hash, F_Pdb), comp.PdbFile, true);

            var duration = DateTime.Now.Subtract(cacheStart);

            if (hm.Duration < duration.TotalMilliseconds)
            {
                // this cached result was slow. record a stat.
                LockStatsCall(() => SlowHitCount++);
                LockStatsCall( () => MSecLost += (int)(duration.TotalMilliseconds - hm.Duration) );
                Logging.Emit("slow cache hit {0}ms", (int)duration.TotalMilliseconds);
            }
            else
            {
                LockStatsCall( () => MSecSaved += (int)(hm.Duration - duration.TotalMilliseconds) );
                Logging.Emit("fast cache hit {0}ms", (int)duration.TotalMilliseconds);
            }

            return 0;
        }

        int CompileWithStreams(IEnumerable<string> args, StreamWriter stderr, StreamWriter stdout, List<string> includes)
        {
            var rv = comp.InvokeCompiler(args,
                        x =>
                        {
                            Console.Error.WriteLine(x);
                            stderr.WriteLine(x);
                        }, y =>
                        {
                            Console.Out.WriteLine(y);
                            stdout.WriteLine(y);
                        }, true, includes);

            return rv;
        }

        public int DoCacheMiss(DataHash hc, IEnumerable<string> args)
        {
            outputCache.EnsureKey(hc.Hash);
            var stderrfile = outputCache.MakePath(hc.Hash, F_Stderr);
            var stdoutfile = outputCache.MakePath(hc.Hash, F_Stdout);
            int rv = -1;
            var ifiles = new List<string>();

            LockStatsCall( () => CacheMisses++);
            
            #region compile
            using (var stderrfs = new StreamWriter(stderrfile))
            {
                using (var stdoutfs = new StreamWriter(stdoutfile))
                {
                    rv = CompileWithStreams(args, stderrfs, stdoutfs, ifiles);
                }
            }
            #endregion

            // we still hold the cache lock, create the manifest asap or give up now!

            if (rv != 0)
            {
                outputCache.ReleaseMutex();
            }
            else
            {
                #region compile succeeded
                var idirs = comp.GetUsedIncludeDirs(ifiles);
                if (idirs.Count < 1)
                {
                    outputCache.ReleaseMutex();
                    throw new InvalidDataException(
                        string.Format("could not find any include folders?! [{0}]",
                        string.Join(" ",args)));
                } else
                {
                    #region process includes folders
                    // save manifest and other things to cache
                    var others = comp.GetPotentialIncludeFiles(idirs, ifiles);
                    var m = new CacheManifest();
                    m.PotentialNewIncludes = others;
                    m.IncludeFiles = new Dictionary<string, string>();
                    m.TimeStamp = DateTime.Now.ToString("s");
                    m.CommonHash = hc.Hash;

                    bool good = true;

                    var hashes = hasher.DigestFiles(ifiles);

                    #region check include files

                    foreach (var x in hashes)
                    {
                        if (x.Value.Result == DataHashResult.Ok)
                        {
                            m.IncludeFiles[x.Key] = x.Value.Hash;
                        }
                        else
                        {
                            Logging.Emit("input hash error {0} {1}", x.Key, x.Value.Result);
                            good = false;
                            m.Disable = true;
                            break;
                        }
                    }

                    #endregion

                    if (!good)
                    {
                        outputCache.ReleaseMutex();
                        LockStatsCall(() => CacheUnsupported++);
                    }
                    else
                    {
                        #region save to cache
                        outputCache.AddFile(hc.Hash, comp.ObjectTarget, F_Object);
                        if (comp.GeneratePdb)
                        {
                            outputCache.AddFile(hc.Hash, comp.PdbFile, F_Pdb);
                            LockStatsCall(() => CacheSize += new FileInfo(comp.PdbFile).Length);
                        }

                        LockStatsCall(() => CacheObjects++);
                        LockStatsCall(() => CacheSize += new FileInfo(comp.ObjectTarget).Length);

                        // write manifest
                        var duration = DateTime.Now.Subtract(cacheStart);
                        m.Duration = (int)duration.TotalMilliseconds;

                        Logging.Emit("cache miss took {0}ms", (int)duration.TotalMilliseconds);

                        var mt = jss.Serialize(m);
                        outputCache.AddTextFileContent(hc.Hash, F_Manifest, mt);

                        outputCache.ReleaseMutex();

                        LockStatsCall(() => CacheSize += mt.Length);
                        #endregion
                    }
                    #endregion
                }
                #endregion
            }
            return rv;
        }

        public int CompileOrCache(IEnumerable<string> args)
        {
            if (IsSupported(args))
            {
                var hc = DeriveHashKey(args);
                if (hc.Result == DataHashResult.Ok)
                {
                    CacheManifest hm;
                    if (CheckCache(hc, out hm))
                    {
                        return DoCacheHit(hc, hm);
                    }
                    else
                    {   // miss, try build
                        return DoCacheMiss(hc, args);
                    }
                }
            }
            else
            {
                LockStatsCall(() => CacheUnsupported++);
            }

            if (comp.ResponseFile != null)
            {
                if (File.Exists(comp.ResponseFile))
                {
                    //var resp = File.ReadAllText(comp.ResponseFile);
                }
            }

            return CompileOnly(args);
        }

        public int CompileOnly(IEnumerable<string> args)
        {
            {
                return comp.InvokeCompiler(args, Console.Error.WriteLine, Console.Out.WriteLine, false, null);
            }
        }

        public void Dispose()
        {
            if (statMtx != null) statMtx.Dispose();
            if (includeCache != null) includeCache.Dispose();
            if (outputCache != null) outputCache.Dispose();
        }
    }
}
