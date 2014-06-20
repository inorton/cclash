using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CClash
{
    public class CompilerCacheFactory
    {
        public static ICompilerCache Get(bool direct, string cachedir, string compiler, string workdir, Dictionary<string,string> envs, out ICompiler comp )
        {
            comp = null;
            ICompilerCache rv = null;
            if (Settings.ServiceMode) {
                try {
                    rv = new CClashServerClient(cachedir);
                } catch (CClashWarningException) {
                    rv = new NullCompilerCache(cachedir);
                }
            }

            if ( rv == null )
            {
                if (direct) {
                    Logging.Emit("use direct mode");
                    rv = new DirectCompilerCache(cachedir);
                } else {
                    throw new NotSupportedException("ppmode is not supported yet");
                }
            }
            comp = rv.SetCompiler(compiler, workdir, envs);
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
        protected String compilerPath;
        protected HashUtil hasher;

        ICacheInfo stats = null;

        protected CompilerCacheBase()
        {
        }

        public CompilerCacheBase(string cacheFolder) : this()
        {
            Logging.Emit("setting up file stores");
            if (string.IsNullOrEmpty(cacheFolder)) throw new ArgumentNullException("cacheFolder");            
            outputCache = FileCacheStore.Load(Path.Combine(cacheFolder, "outputs"));
            includeCache = FileCacheStore.Load(Path.Combine(cacheFolder, "includes"));
            Logging.Emit("setup cache info");
            stats = new CacheInfo(outputCache);
            Logging.Emit("setup hasher");
            hasher = new HashUtil(includeCache);
        }

        public abstract void Setup();
        public abstract void Finished();

        public ICompiler SetCompilerEx(int parentpid, string compiler, string workdir, Dictionary<string, string> envs)
        {
            var comp = SetCompiler(compiler, workdir, envs);
            comp.ParentPid = parentpid;
            return comp;
        }

        public ICompiler SetCompiler(string compiler, string workdir, Dictionary<string,string> envs)
        {
            if (string.IsNullOrEmpty(compiler)) throw new ArgumentNullException("compiler");
            
            compilerPath = System.IO.Path.GetFullPath(compiler);
            var comp = new Compiler()
            {
                CompilerExe = compilerPath
            };
            comp.SetWorkingDirectory(workdir);
            comp.SetEnvironment(envs);

            return comp;
        }

        public virtual bool IsSupported(ICompiler comp, IEnumerable<string> args)
        {
            if (FileUtils.Exists(compilerPath))
            {
                var rv = comp.ProcessArguments(args.ToArray());
                if (!rv)
                {
                    Logging.Emit("unsupported args: {0}", string.Join(" ",args.ToArray()));
                }
                else
                {
                    Logging.Input(comp.WorkingDirectory, comp.ObjectTarget, args);
                }
                return rv;
            }
            throw new FileNotFoundException(compilerPath);
        }

        public virtual DataHash DigestBinaryFile(string path)
        {
            return hasher.DigestBinaryFile(path);
        }

        public virtual DataHash DigestCompiler(string compilerPath)
        {
            
            return DigestBinaryFile(compilerPath);
        }

        public DataHash DeriveHashKey( ICompiler comp, IEnumerable<string> args)
        {
            Logging.Emit("compiler is {0}", comp.CompilerExe);
            var comphash = DigestCompiler(comp.CompilerExe);
            if (comphash.Result == DataHashResult.Ok)
            {
                var srchash = hasher.DigestBinaryFile(comp.SingleSourceFile);
                if (srchash.Result == DataHashResult.Ok)
                {
                    var buf = new StringBuilder();
                    buf.AppendLine(CacheInfo.CacheFormat);
                    buf.AppendLine(srchash.Hash);
                    buf.AppendLine(comp.WorkingDirectory);
                    string incs = null;
                    comp.EnvironmentVariables.TryGetValue("INCLUDE", out incs);
                    if (incs != null) buf.AppendLine(incs);

                    foreach (var a in args)
                        buf.AppendLine(a);
                    buf.AppendLine(comphash.Hash);

                    comphash = hasher.DigestString(buf.ToString());
                }
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

        void CopyOutputFiles( ICompiler comp, DataHash hc)
        {
            try
            {
                CopyFile(outputCache.MakePath(hc.Hash, F_Object), comp.ObjectTarget);
                if (comp.GeneratePdb && comp.AttemptPdb && comp.PdbFile != null)
                    CopyFile(outputCache.MakePath(hc.Hash, F_Pdb), comp.PdbFile);
            }
            catch (Exception e)
            {
                Logging.Error("{0}",e);
                throw;
            }
        }

        void CopyStdio(ICompiler comp, DataHash hc)
        {
            var stderrfile = outputCache.MakePath(hc.Hash, F_Stderr);
            var stdoutfile = outputCache.MakePath(hc.Hash, F_Stdout);
            comp.StdOutputCallback(File.ReadAllText(stdoutfile));
            comp.StdErrorCallback(File.ReadAllText(stderrfile));
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

        protected int OnCacheHitLocked(ICompiler comp, DataHash hc, CacheManifest hm)
        {
            CopyStdio(comp, hc);
            CopyOutputFiles(comp, hc);

            // we dont need the lock now, it is highly unlikley someone else will
            // modify these files
            Unlock(CacheLockType.Read);

            var duration = comp.Age;

            var tstat = Task.Run(() =>
            {
                Stats.LockStatsCall(() =>
                    {
                        Stats.CacheHits++;
                        if (hm.Duration < duration.TotalMilliseconds)
                        {
                            // this cached result was slow. record a stat.

                            Stats.SlowHitCount++;
                            Logging.Emit("slow cache hit {0}ms", (int)duration.TotalMilliseconds);
                        }
                        else
                        {
                            Logging.Emit("fast cache hit {0}ms", (int)duration.TotalMilliseconds);
                        }
                    });
            });

            tstat.Wait();
            return 0;
        }

        public abstract bool CheckCache(ICompiler comp, IEnumerable<string> args, DataHash commonkey, out CacheManifest manifest);
        protected abstract int OnCacheMissLocked(ICompiler comp, DataHash hc, IEnumerable<string> args, CacheManifest m);

        protected int CompileWithStreams(ICompiler comp, IEnumerable<string> args, StreamWriter stderr, StreamWriter stdout, List<string> includes)
        {
            var rv = comp.InvokeCompiler(args,
                        x =>
                        {
                            stderr.Write(x);
                        }, y =>
                        {
                            stdout.Write(y);
                        }, includes != null, includes);

            return rv;
        }

        public virtual int CompileOrCache( ICompiler comp, IEnumerable<string> args)
        {
            if (IsSupported(comp, args))
            {
                args = comp.CompileArgs;
                var hc = DeriveHashKey(comp, args);
                if (hc.Result == DataHashResult.Ok)
                {
                    CacheManifest hm;
                    if (CheckCache(comp, args ,hc, out hm))
                    {
                        Logging.Hit(hc.Hash, comp.WorkingDirectory, comp.ObjectTarget);
                        return OnCacheHitLocked(comp, hc, hm);
                    }
                    else
                    {   // miss, try build
                        return OnCacheMissLocked(comp, hc, args, hm);
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

            return CompileOnly(comp, args);
        }

        public int CompileOnly(ICompiler comp, IEnumerable<string> args)
        {
            {
                return comp.InvokeCompiler(args, null, null, false, null);
            }
        }


        public void SetCaptureCallback(ICompiler comp, Action<string> onOutput, Action<string> onError)
        {
            comp.StdErrorCallback = onError;
            comp.StdOutputCallback = onOutput;
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

        public virtual void Lock(CacheLockType mode)
        {
            outputCache.WaitOne();
        }

        public virtual void Unlock(CacheLockType mode)
        {
            outputCache.ReleaseMutex();
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
