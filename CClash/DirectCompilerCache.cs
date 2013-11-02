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
    public class DirectCompilerCache : CompilerCacheBase, ICompilerCache
    {
        public DirectCompilerCache(string cacheFolder)
            : base(cacheFolder)
        {
        }

        public virtual Dictionary<string, DataHash> GetHashes( IEnumerable<string> fnames )
        {
            return hasher.DigestFiles(fnames);
        }

        public virtual bool FileExists(string path)
        {
            return FileUtils.Exists(path);
        }

        /// <summary>
        /// When this returns, we will hold the output cache mutex.
        /// </summary>
        /// <param name="commonkey"></param>
        /// <param name="manifest"></param>
        /// <returns></returns>
        protected override bool CheckCache( IEnumerable<string> args, DataHash commonkey, out CacheManifest manifest )
        {
            manifest = null;
            outputCache.WaitOne();
            manifest = GetCachedManifestLocked(commonkey);
            if (manifest != null)
            {
                if (manifest.Disable)
                {
                    Logging.Emit("disabled by manifest");
                    return false;
                }
                foreach (var f in manifest.PotentialNewIncludes)
                {
                    if (FileExists(f))
                    {
                        Logging.Emit("detected added include file {0}", f);
                        return false;
                    }
                }
                var hashes = GetHashes(manifest.IncludeFiles.Keys);

                foreach (var h in hashes)
                {
                    if (h.Value.Result == DataHashResult.Ok)
                    {

                        if (manifest.IncludeFiles[h.Key] != h.Value.Hash)
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

        protected override int OnCacheMissLocked(DataHash hc, IEnumerable<string> args, CacheManifest m)
        {
            Logging.Emit("cache miss");
            outputCache.EnsureKey(hc.Hash);
            var stderrfile = outputCache.MakePath(hc.Hash, F_Stderr);
            var stdoutfile = outputCache.MakePath(hc.Hash, F_Stdout);
            int rv = -1;
            var ifiles = new List<string>();

            Stats.LockStatsCall(() => Stats.CacheMisses++);
            
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
                    m = new CacheManifest();
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
                    }
                    else
                    {
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

                        outputCache.ReleaseMutex();

                        Stats.LockStatsCall(() => Stats.CacheSize += mt.Length);
                        #endregion
                    }
                    #endregion
                }
                #endregion
            }
            return rv;
        }

    }
}
