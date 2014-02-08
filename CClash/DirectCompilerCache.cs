using System;
using System.Collections.Generic;
using System.IO;

namespace CClash
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly")]
    public class DirectCompilerCache : CompilerCacheBase, ICompilerCache
    {
        public DirectCompilerCache(string cacheFolder)
            : base(cacheFolder)
        {
        }

        public override void Setup()
        {
        }

        public override void Finished()
        {
        }

        protected virtual bool CheckPotentialIncludes(IEnumerable<string> potentials, ICompiler comp)
        {
            foreach (var f in potentials)
            {
                if (!FileUtils.FileMissing(f))
                {
                    Logging.Emit("detected added include file {0}", f);
                    Logging.Miss(DataHashResult.FileAdded, "", comp.SingleSourceFile, f);
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// When this returns, we will hold the output cache mutex.
        /// </summary>
        /// <param name="commonkey"></param>
        /// <param name="manifest"></param>
        /// <returns></returns>
        protected override bool CheckCache( ICompiler comp, IEnumerable<string> args, DataHash commonkey, out CacheManifest manifest )
        {
            manifest = null;
            outputCache.WaitOne();
            manifest = GetCachedManifestLocked(commonkey);
            if (manifest != null && !string.IsNullOrEmpty(manifest.ObjectHash))
            {
                if (manifest.Disable)
                {
                    Logging.Emit("disabled by manifest");
                    return false;
                }


                if (!CheckPotentialIncludes(manifest.PotentialNewIncludes, comp))
                    return false;

                var hashes = GetHashes(manifest.IncludeFiles.Keys);

                foreach (var h in hashes)
                {
                    if (h.Value.Result == DataHashResult.Ok)
                    {
                        string mhash;
                        if (manifest.IncludeFiles.TryGetValue(h.Key, out mhash))
                        {
                            if (mhash != h.Value.Hash)
                            {
                                Logging.Emit("include file hash changed {0}", h.Key);
                                Logging.Miss(DataHashResult.FileChanged, "", comp.SingleSourceFile, h.Key);
                                return false;
                            }
                        }
                        else
                        {
                            Logging.Emit("include file added {0}", h.Key);
                            Logging.Miss(DataHashResult.FileAdded, "", comp.SingleSourceFile, h.Key);
                            return false;
                        }
                    }
                    else
                    {
                        Logging.Emit("include file hash error {0} {1}", h.Key, h.Value.Result);
                        Logging.Miss(h.Value.Result, "", comp.SingleSourceFile, h.Key);
                        return false;
                    }
                }

                foreach (var f in new string[] { F_Manifest, F_Stderr, F_Stdout })
                {
                    if (!FileUtils.Exists(outputCache.MakePath(commonkey.Hash, f)))
                    {
                        outputCache.Remove(commonkey.Hash);
                        Logging.Miss(DataHashResult.CacheCorrupt, commonkey.Hash, comp.SingleSourceFile, "");
                        return false;
                    }
                }

                var hasobj = objectCache.ContainsEntry( manifest.ObjectHash, F_Object );
                if ( hasobj && comp.GeneratePdb)
                {
                    return objectCache.ContainsEntry(manifest.PdbHash, F_Pdb);
                }
                return hasobj;
            }
            Logging.Miss(DataHashResult.NoPreviousBuild, Directory.GetCurrentDirectory(), comp.SingleSourceFile, "");
            return false;
        }

        TimeSpan lastCompileDuration = default(TimeSpan);

        void WriteIO(StreamWriter w, string str, TextWriter console, bool echo)
        {
            if (echo)
                if (console != null)
                    console.WriteLine(str);
            w.WriteLine(str);
        }

        protected int Compile(ICompiler comp, IEnumerable<string> args, string stderrfile, string stdoutfile, List<string> includes, bool echoConsole)
        {
            using (var stderrfs = new StreamWriter(stderrfile))
            {
                using (var stdoutfs = new StreamWriter(stdoutfile))
                {
                    outputCache.ReleaseMutex();
                    try {
                        int rv = comp.InvokeCompiler(args, (x) => WriteIO(stderrfs, x, Console.Error, echoConsole),
                            (y) => WriteIO(stdoutfs, y, Console.Out, echoConsole), includes != null, includes);
                        return rv;
                    } finally {
                        outputCache.WaitOne();
                    }
                }
            }
        }

        protected virtual int Compile(ICompiler comp, IEnumerable<string> args, string stderrfile, string stdoutfile, List<string> includes)
        {
            return Compile(comp, args, stderrfile, stdoutfile, includes, true);
        }

        protected virtual void SaveOutputsLocked(ICompiler comp, CacheManifest m)
        {
            var objhash = DigestBinaryFile(comp.ObjectTarget);
            m.ObjectHash = objhash.Hash;
            if (!objectCache.ContainsEntry(objhash.Hash, F_Object))
            {
                objectCache.AddFile(objhash.Hash, comp.ObjectTarget, F_Object);
                Stats.LockStatsCall(() => Stats.CacheObjects++);
                Stats.LockStatsCall(() => Stats.CacheSize += new FileInfo(comp.ObjectTarget).Length);
            }

            if (comp.GeneratePdb)
            {
                var pdbhash = DigestBinaryFile(comp.PdbFile);
                m.PdbHash = pdbhash.Hash;
                if (!objectCache.ContainsEntry(pdbhash.Hash, F_Pdb))
                {
                    objectCache.AddFile(pdbhash.Hash, comp.PdbFile, F_Pdb);
                    Stats.LockStatsCall(() => Stats.CacheSize += new FileInfo(comp.PdbFile).Length);
                }
            }

            // write manifest
            var duration = DateTime.Now.Subtract(cacheStart);
            m.Duration = (int)duration.TotalMilliseconds;

            Logging.Emit("cache miss took {0}ms", (int)duration.TotalMilliseconds);

            var fname = outputCache.MakePath(m.CommonHash, F_Manifest);
            using (var fs = new FileStream(fname, FileMode.OpenOrCreate, FileAccess.Write))
            {
                m.Serialize(fs);
            }
        }

        protected override int OnCacheMissLocked(DataHash hc, ICompiler comp, IEnumerable<string> args, CacheManifest m, Action<string> stderr, Action<string> stdout)
        {
            Logging.Emit("cache miss");
            outputCache.EnsureKey(hc.Hash);
            var stderrfile = outputCache.MakePath(hc.Hash, F_Stderr);
            var stdoutfile = outputCache.MakePath(hc.Hash, F_Stdout);
            var ifiles = new List<string>();
            Stats.LockStatsCall(() => Stats.CacheMisses++);

            int rv = Compile(comp, args, stderrfile, stdoutfile, ifiles );

            // we still hold the cache lock, create the manifest asap or give up now!

            if (rv != 0)
            {
                outputCache.ReleaseMutex();
            }
            else
            {
                // this unlocks for us
                DoCacheMiss(comp, hc, args, m, ifiles);
            }

            var duration = DateTime.Now.Subtract(cacheStart);
            var wasted = duration - lastCompileDuration;
            // estimate about 40% overhead was a waste.
            Stats.LockStatsCall(() => Stats.MSecLost += (int)wasted.TotalMilliseconds);

            return rv;
        }

        protected virtual void DoCacheMiss( ICompiler c, DataHash hc, IEnumerable<string> args, CacheManifest m, List<string> ifiles)
        {

            var idirs = c.GetUsedIncludeDirs(ifiles);
            if (idirs.Count < 1)
            {
                outputCache.ReleaseMutex();
                throw new InvalidDataException(
                    string.Format("could not find any include folders?! [{0}]",
                    string.Join(" ", args)));
            }
            else
            {
                #region process includes folders
                // save manifest and other things to cache

                List<string> others;
                if (Settings.BypassPotentialIncludeChecks)
                {
                    others = new List<string>();
                }
                else
                {
                    others = c.GetPotentialIncludeFiles(idirs, ifiles);
                }


                m = new CacheManifest();
                m.PotentialNewIncludes = others;
                m.IncludeFiles = new Dictionary<string, string>();
                m.TimeStamp = DateTime.Now.ToString("s");
                m.CommonHash = hc.Hash;

                #endregion

                bool good = true;

                var hashes = GetHashes(ifiles);

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

                if (good)
                {
                    SaveOutputsLocked(c, m);
                }
                outputCache.ReleaseMutex();

            }

        }

    }
}
