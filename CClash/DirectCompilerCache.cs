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

        public FileCacheStore OutputCache
        {
            get
            {
                return outputCache;
            }
        }

        public override void Setup()
        {
        }

        public override void Finished()
        {
        }

        /// <summary>
        /// When this returns, we will hold the output cache mutex.
        /// </summary>
        /// <param name="commonkey"></param>
        /// <param name="manifest"></param>
        /// <returns></returns>
        public override bool CheckCache(ICompiler comp, IEnumerable<string> args, DataHash commonkey, out CacheManifest manifest )
        {
            manifest = null;
            Lock(CacheLockType.Read);
            manifest = GetCachedManifestLocked(commonkey);
            if (manifest != null)
            {
                #region build missed before
                if (manifest.Disable)
                {
                    Logging.Emit("disabled by manifest");
                    return false;
                }
                #region check includes
                foreach (var f in manifest.PotentialNewIncludes)
                {
                    if (!FileUtils.FileMissing(f))
                    {
                        Logging.Emit("detected added include file {0}", f);
                        Logging.Miss(commonkey.Hash, DataHashResult.FileAdded, Directory.GetCurrentDirectory(), comp.SingleSourceFile, f);
                        return false;
                    }
                }
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
                                Logging.Miss(commonkey.Hash, DataHashResult.FileChanged, Directory.GetCurrentDirectory(), comp.SingleSourceFile, h.Key);
                                return false;
                            }
                        }
                        else
                        {
                            Logging.Emit("include file added {0}", h.Key);
                            Logging.Miss(commonkey.Hash, DataHashResult.FileAdded, Directory.GetCurrentDirectory(), comp.SingleSourceFile, h.Key);
                            return false;
                        }
                    }
                    else
                    {
                        Logging.Emit("include file hash error {0} {1}", h.Key, h.Value.Result);
                        Logging.Miss(commonkey.Hash, h.Value.Result, Directory.GetCurrentDirectory(), comp.SingleSourceFile, h.Key);
                        return false;
                    }
                }
                #endregion

                #region check pdb
                if (comp.AttemptPdb)
                {
                    if (comp.PdbExistsAlready)
                    {
                        var pdbhash = hasher.DigestBinaryFile(comp.PdbFile);
                        if (pdbhash.Hash != manifest.EarlierPdbHash)
                        {
                            outputCache.Remove(commonkey.Hash);
                            Logging.Miss(commonkey.Hash, DataHashResult.FileChanged, commonkey.Hash, comp.PdbFile, "");
                            return false;
                        }
                    }
                }
                #endregion

                #region check cached data exists
                foreach (var f in new string[] { F_Manifest, F_Object })
                {
                    if (!FileUtils.Exists(outputCache.MakePath(commonkey.Hash, f)))
                    {
                        outputCache.Remove(commonkey.Hash);
                        Logging.Miss(commonkey.Hash, DataHashResult.CacheCorrupt, commonkey.Hash, comp.SingleSourceFile, "");
                        return false;
                    }
                }
                #endregion
                if (Settings.MissLogEnabled)
                {
                    Logging.Emit("hit hc={0},dir={1},src={2}", commonkey.Hash, comp.WorkingDirectory, comp.SingleSourceFile);
                }
                return true; // cache hit, all includes match and no new files added
                #endregion
            }

            Logging.Miss(commonkey.Hash, DataHashResult.NoPreviousBuild, comp.WorkingDirectory, comp.SingleSourceFile, "");
            return false;
        }

        TimeSpan lastCompileDuration = default(TimeSpan);

        protected virtual int Compile(ICompiler comp, IEnumerable<string> args, string stderrfile, string stdoutfile, List<string> includes)
        {
            #region compile
            var start = DateTime.Now;
            using (var stderrfs = new StreamWriter(stderrfile))
            {
                using (var stdoutfs = new StreamWriter(stdoutfile))
                {
                    var rv = CompileWithStreams(comp, args, stderrfs, stdoutfs, includes);
                    lastCompileDuration = DateTime.Now.Subtract(start);
                    return rv;
                }
            }
            #endregion
        }

        protected virtual void SaveOutputsLocked(CacheManifest m, ICompiler c )
        {
            outputCache.AddFile(m.CommonHash, c.ObjectTarget, F_Object);
            if (c.GeneratePdb)
            {
                var pdbhash = hasher.DigestBinaryFile(c.PdbFile);
                m.PdbHash = pdbhash.Hash;
                outputCache.AddFile(m.CommonHash, c.PdbFile, F_Pdb);
                Stats.LockStatsCall(() => Stats.CacheSize += new FileInfo(c.PdbFile).Length);
            }

            Stats.LockStatsCall(() => Stats.CacheObjects++);
            Stats.LockStatsCall(() => Stats.CacheSize += new FileInfo(c.ObjectTarget).Length);

            // write manifest
            var duration = c.Age;
            m.Duration = (int)duration.TotalMilliseconds;

            Logging.Emit("cache miss took {0}ms", (int)duration.TotalMilliseconds);

            var fname = outputCache.MakePath(m.CommonHash, F_Manifest);
            using (var fs = new FileStream(fname, FileMode.OpenOrCreate, FileAccess.Write))
            {
                m.Serialize(fs);
            }
        }

        protected override int OnCacheMissLocked(ICompiler comp, DataHash hc, IEnumerable<string> args, CacheManifest m)
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
                Unlock(CacheLockType.Read);
            }
            else
            {
                // this unlocks for us
                try
                {
                    DoCacheMiss(comp, hc, args, m, ifiles);
                }
                catch (CClashWarningException)
                {
                    return CompileOnly(comp, args);
                }
            }

            return rv;
        }

        protected virtual void DoCacheMiss(ICompiler c, DataHash hc, IEnumerable<string> args, CacheManifest m, List<string> ifiles)
        {
            bool good = true;
            try
            {
                var idirs = c.GetUsedIncludeDirs(ifiles);
                if (idirs.Count < 1)
                {
                    throw new InvalidDataException(
                        string.Format("could not find any include folders?! [{0}]",
                        string.Join(" ", args)));
                }
                #region process includes folders
                // save manifest and other things to cache
                var others = c.GetPotentialIncludeFiles(idirs, ifiles);
                m = new CacheManifest();
                m.PotentialNewIncludes = others;
                m.IncludeFiles = new Dictionary<string, string>();
                m.TimeStamp = DateTime.Now.ToString("s");
                m.CommonHash = hc.Hash;

                #endregion

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
                        Logging.Miss(hc.Hash, x.Value.Result, c.WorkingDirectory, c.SingleSourceFile, x.Key);
                        good = false;
                        m.Disable = true;
                        break;
                    }
                }

                #endregion
            }
            finally
            {
                Unlock(CacheLockType.Read);
                if (good)
                {
                    Lock(CacheLockType.ReadWrite);
                    try
                    {
                        SaveOutputsLocked(m, c);
                    }
                    finally
                    {
                        Unlock(CacheLockType.ReadWrite);
                    }
                }
            }
        }
    }
}
