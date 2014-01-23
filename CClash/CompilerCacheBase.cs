using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public class CompilerCacheFactory
    {
        public static ICompilerCache Get(bool direct, string cachedir )
        {
            ICompilerCache rv;
            if (Settings.ServiceMode)
            {
                rv = new CClashServerClient(cachedir);
            }
            else
            {

                if (direct)
                {
                    Logging.Emit("use direct mode");
                    rv = new DirectCompilerCache(cachedir);
                }
                else
                {
                    throw new NotSupportedException("ppmode is not supported yet");
                }
            }
            return rv;
        }
    }

    public abstract class CompilerCacheBase : IDisposable
    {
        public const string F_Manifest = "manifest.bin";
        public const string F_Object = "target.object";
        public const string F_Pdb = "target.pdb";
        public const string F_Stdout = "compiler.stdout";
        public const string F_Stderr = "compiler.stderr";

        protected FileCacheStore outputCache;
        protected FileCacheStore includeCache;
        protected FileCacheStore objectCache;
        
        protected HashUtil hasher;
        
        ICacheInfo stats = null;

        protected static DateTime cacheStart = DateTime.Now;

        public CompilerCacheBase(string cacheFolder)
        {
            if (string.IsNullOrEmpty(cacheFolder)) throw new ArgumentNullException("cacheFolder");            
            outputCache = FileCacheStore.Load(Path.Combine(cacheFolder, "outputs"));
            objectCache = FileCacheStore.Load(Path.Combine(cacheFolder, "objects"));
            includeCache = FileCacheStore.Load(Path.Combine(cacheFolder, "includes"));
            stats = new CacheInfo(outputCache);
            hasher = new HashUtil(includeCache);
        }

        public abstract void Setup();
        public abstract void Finished();

        public ICompiler GetCompiler(string compiler, string workdir, IDictionary<string,string> envs)
        {
            return new Compiler(compiler, workdir, envs);
        }

        public virtual bool IsSupported( ICompiler comp, IEnumerable<string> args)
        {
            cacheStart = DateTime.Now;
            var rv = comp.ProcessArguments(args.ToArray());
            if (!rv)
            {
                Logging.Emit("args not supported");
            }
            return rv;
        }

        public virtual DataHash DigestBinaryFile(string path)
        {
            return hasher.DigestBinaryFile(path);
        }

        public virtual DataHash DigestCompiler(string compilerPath)
        {
            return DigestBinaryFile(compilerPath);
        }

        public DataHash DeriveHashKey(ICompiler comp, IEnumerable<string> args)
        {
            var comphash = DigestCompiler(comp.CompilerExe);
            if (comphash.Result == DataHashResult.Ok)
            {
                var buf = new StringBuilder();
                buf.AppendLine(this.GetType().FullName.ToString());
                var incs = comp.Envs["INCLUDE"];
                if (incs != null)
                    buf.AppendLine(incs);
                foreach (var a in args)
                    buf.AppendLine(a);
                buf.AppendLine(comphash.Hash);
                comphash = hasher.DigestString(buf.ToString());
            }
            return comphash;
        }

        protected CacheManifest GetCachedManifestLocked(DataHash commonkey)
        {
            CacheManifest manifest = null;
            
            if (outputCache.ContainsEntry(commonkey.Hash, F_Manifest))
            {
                var mn = outputCache.MakePath(commonkey.Hash, F_Manifest);
                using ( var fs = new FileStream( mn, FileMode.Open ) ){
                    manifest = CacheManifest.Deserialize(fs);
                }
            }

            return manifest;
        }

        public void CopyOrHardLink(string src, string dst)
        {
            if (!Settings.TryHardLinks || !FileUtils.TryHardLink(src, dst))
            {
                CopyFile(src, dst);
            }
        }

        protected virtual void CopyOutputFiles(CacheManifest hm, string objpath, string pdbpath)
        {
            try
            {
                var cachedobj = objectCache.MakePath(hm.ObjectHash, F_Object);
                CopyOrHardLink(cachedobj, objpath);
                
                if (!string.IsNullOrWhiteSpace(pdbpath))
                {
                    var cachedpdb = objectCache.MakePath(hm.PdbHash, F_Pdb);
                    CopyOrHardLink(cachedpdb, pdbpath);
                }
            }
            catch (Exception e)
            {
                Logging.Error("{0}",e);
                throw;
            }
        }

        void CopyStdio(DataHash hc, Action<string> stderr, Action<string> stdout)
        {
            var stderrfile = outputCache.MakePath(hc.Hash, F_Stderr);
            var stdoutfile = outputCache.MakePath(hc.Hash, F_Stdout);

            if (stderr != null) stderr(File.ReadAllText(stderrfile));
            if (stdout != null) stdout(File.ReadAllText(stdoutfile));
        }

        public virtual Dictionary<string, DataHash> GetHashes(IEnumerable<string> fnames)
        {
            return hasher.DigestFiles(fnames);
        }

        public virtual bool FileExists(string path)
        {
            return FileUtils.Exists(path);
        }

        public virtual bool FileMissing(string path)
        {
            return FileUtils.FileMissing(path);
        }

        public virtual void CopyFile(string from, string to)
        {
            FileUtils.CopyUnlocked(from, to);
        }

        protected int OnCacheHitLocked(DataHash hc, ICompiler comp, CacheManifest hm, Action<string> stderr, Action<string> stdout)
        {
            // we dont need the lock now, it is highly unlikley someone else will
            // modify these files
            outputCache.ReleaseMutex();

            CopyStdio(hc, stderr, stdout);
            CopyOutputFiles(hm, comp.ObjectTarget, comp.PdbFile);

            var duration = DateTime.Now.Subtract(cacheStart);

            var tstat = Task.Run(() =>
            {
                Stats.LockStatsCall(() =>
                    {
                        Stats.CacheHits++;
                        if (hm.Duration < duration.TotalMilliseconds)
                        {
                            // this cached result was slow. record a stat.

                            Stats.SlowHitCount++;
                            Stats.MSecLost += (int)(duration.TotalMilliseconds - hm.Duration);

                            Logging.Emit("slow cache hit {0}ms", (int)duration.TotalMilliseconds);
                        }
                        else
                        {
                            Logging.Emit("fast cache hit {0}ms", (int)duration.TotalMilliseconds);
                            Stats.MSecSaved += (int)(hm.Duration - duration.TotalMilliseconds);
                        }
                    });
            });

            tstat.Wait();
            return 0;
        }

        protected abstract bool CheckCache(ICompiler comp, IEnumerable<string> args, DataHash commonkey, out CacheManifest manifest);
        protected abstract int OnCacheMissLocked(DataHash hc, ICompiler comp, IEnumerable<string> args, CacheManifest m, Action<string> stderr, Action<string> stdout);

        protected int CompileWithStreams(ICompiler comp, IEnumerable<string> args, StreamWriter stderr, StreamWriter stdout, List<string> includes)
        {
            bool saveincludes = includes != null;
            var rv = comp.InvokeCompiler(args,
                        x =>
                        {
                            if (!saveincludes)
                            {
                                ErrorWriteLine(x);
                            }
                            stderr.WriteLine(x);
                        }, y =>
                        {
                            if (!saveincludes)
                            {
                                OutputWriteLine(y);
                            }
                            stdout.WriteLine(y);
                            
                        }, saveincludes, includes);

            return rv;
        }

        public virtual int CompileOrCache(ICompiler comp, IEnumerable<string> args, Action<string> stderr, Action<string> stdout)
        {
            if (IsSupported(comp, args))
            {
                var hc = DeriveHashKey(comp, args);
                if (hc.Result == DataHashResult.Ok)
                {
                    CacheManifest hm;
                    if (CheckCache(comp, args, hc, out hm))
                    {
                        return OnCacheHitLocked(hc, comp, hm, stderr, stdout);
                    }
                    else
                    {   // miss, try build
                        return OnCacheMissLocked(hc, comp, args, hm, stderr, stdout);
                    }
                }
            }
            else
            {
                Stats.LockStatsCall(() => Stats.CacheUnsupported++);
            }

            if (comp.ResponseFile != null)
            {
                if (File.Exists(comp.ResponseFile))
                {
                    //var resp = File.ReadAllText(comp.ResponseFile);
                }
            }

            return comp.InvokeCompiler(args, stderr, stdout, false, null);
        }

        public virtual void ErrorWriteLine(string str)
        {
            Console.Error.WriteLine(str);
        }

        public virtual void OutputWriteLine(string str)
        {
            Console.Out.WriteLine(str);
        }

        public virtual void OutputWrite(string str)
        {
            Console.Out.Write(str);
        }

        public virtual void ErrorWrite(string str)
        {
            Console.Error.Write(str);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (stats != null) stats.Dispose();
                if (includeCache != null) includeCache.Dispose();
                if (outputCache != null) outputCache.Dispose();
            }
        }


        public ICacheInfo Stats
        {
            get
            {
                return stats;
            }
            protected set
            {
                stats = value;
            }
        }


    }
}
