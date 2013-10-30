using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CClash
{
    public sealed class PreprocessorBasedCompilerCache : CompilerCacheBase, ICompilerCache
    {
        public PreprocessorBasedCompilerCache(string cacheFolder, string compiler)
            : base(cacheFolder, compiler)
        {
        }

        Thread compilerThread = null;

        protected override bool CheckCache(IEnumerable<string> args, DataHash commonkey, out CacheManifest m)
        {
            outputCache.WaitOne();
            outputCache.EnsureKey(commonkey.Hash);
            m = GetCachedManifestLocked(commonkey);
            bool ppmatch = false;
            
           
            if (m == null)
            {
                m = new CacheManifest()
                {
                    CommonHash = commonkey.Hash,
                    TimeStamp = DateTime.Now.ToString("s"),
                    ExitCode = -1,
                };
                CacheManifest tmp = m;
                // not previously built, do the cache miss in the background while we run the preprocessor
                compilerThread = new Thread(() =>
                {
                    tmp.ExitCode = CacheMissLockedInternal(commonkey, args, tmp);
                });
                compilerThread.Start();
            }

            if (m.Disable)
            {
                Logging.Emit("disabled by manifest");
                return false;
            }

            // have the preprocessor output, lets run the preprocessor in /E mode
            using (var ms = new MemoryStream(2 * 1024))
            {
                var st = DateTime.Now;
                int rv = -1;
                using (var sw = new StreamWriter(ms))
                {
                    rv = comp.InvokePreprocessor(sw);

                    if (rv == 0)
                    {
                        ms.Seek(0, SeekOrigin.Begin);
                        var h = hasher.DigestStream(ms);
                        if (h.Result == DataHashResult.Ok)
                        {
                            if (!string.IsNullOrEmpty(m.PreprocessedSourceHash) && h.Hash == m.PreprocessedSourceHash)
                            {
                                ppmatch = true;
                            }
                            else
                            {
                                m.PreprocessedSourceHash = h.Hash;
                            }
                        }
                    }
                }
                var et = DateTime.Now;
                Logging.Emit("preprocessor took {0}ms", et.Subtract(st).TotalMilliseconds);

            }

            return ppmatch;
        }

        protected override int OnCacheMissLocked(DataHash hc, IEnumerable<string> args, CacheManifest m)
        {
            if (compilerThread != null)
            {
                compilerThread.Join();
                compilerThread = null;
            }
            outputCache.ReleaseMutex();

            return m.ExitCode;
        }

        int CacheMissLockedInternal(DataHash hc, IEnumerable<string> args, CacheManifest m)
        {
            Logging.Emit("cache miss");
            
            var stderrfile = outputCache.MakePath(hc.Hash, F_Stderr);
            var stdoutfile = outputCache.MakePath(hc.Hash, F_Stdout);
            int rv = -1;
            
            Stats.LockStatsCall(() => Stats.CacheMisses++);

            #region compile
            var st = DateTime.Now;
            using (var stderrfs = new StreamWriter(stderrfile))
            {
                using (var stdoutfs = new StreamWriter(stdoutfile))
                {
                    rv = CompileWithStreams(args, stderrfs, stdoutfs, null);
                }
            }
            var et = DateTime.Now;
            Logging.Emit("compile took {0}ms", et.Subtract(st).TotalMilliseconds);
            #endregion

            if (rv == 0)
            {
                // save the object and/or pdb
                #region save to cache
                outputCache.AddFile(hc.Hash, comp.ObjectTarget, F_Object);
                if (comp.GeneratePdb)
                {
                    outputCache.AddFile(hc.Hash, comp.PdbFile, F_Pdb);
                    Stats.LockStatsCall(() => Stats.CacheSize += new FileInfo(comp.PdbFile).Length);
                }

                Stats.LockStatsCall(() => Stats.CacheObjects++);
                Stats.LockStatsCall(() => Stats.CacheSize += new FileInfo(comp.ObjectTarget).Length);

                // write manifest
                var duration = DateTime.Now.Subtract(cacheStart);
                m.Duration = (int)duration.TotalMilliseconds;

                Logging.Emit("cache miss took {0}ms", (int)duration.TotalMilliseconds);

                var mt = jss.Serialize(m);
                outputCache.AddTextFileContent(hc.Hash, F_Manifest, mt);

                Stats.LockStatsCall(() => Stats.CacheSize += mt.Length);
                #endregion
            }

            return rv;
        }
    }
}
