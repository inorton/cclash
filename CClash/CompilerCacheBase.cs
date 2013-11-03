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
        public static ICompilerCache Get(bool direct, string cachedir, string compiler )
        {
            ICompilerCache rv;
            if (Settings.ServiceMode)
            {
                return new CClashServerClient(cachedir);
            }

            if (direct)
            {
                Logging.Emit("use direct mode");
                rv = new DirectCompilerCache(cachedir);
            }
            else
            {
                Logging.Emit("use pp cache");
                rv = new PreprocessorBasedCompilerCache(cachedir);
            }
            rv.SetCompiler(compiler);
            return rv;
        }
    }

    public abstract class CompilerCacheBase
    {
        public const string F_Manifest = "manifest.json";
        public const string F_Object = "target.object";
        public const string F_Pdb = "target.pdb";
        public const string F_Stdout = "compiler.stdout";
        public const string F_Stderr = "compiler.stderr";

        protected FileCacheStore outputCache;
        protected FileCacheStore includeCache;
        protected String compilerPath;
        protected HashUtil hasher;
        protected Compiler comp;

        public Compiler Compiler
        {
            get { return comp; }
            set { comp = value; }
        }

        CacheStats stats = null;

        protected static DateTime cacheStart = DateTime.Now;

        protected JavaScriptSerializer jss = new JavaScriptSerializer();


        public CompilerCacheBase(string cacheFolder)
        {
            if (string.IsNullOrEmpty(cacheFolder)) throw new ArgumentNullException("cacheFolder");            
            outputCache = FileCacheStore.Load(Path.Combine(cacheFolder, "outputs"));
            includeCache = FileCacheStore.Load(Path.Combine(cacheFolder, "includes"));
            stats = new CacheStats(outputCache);
            hasher = new HashUtil(includeCache);
        }

        public void SetCompiler(string compiler)
        {
            if (string.IsNullOrEmpty(compiler)) throw new ArgumentNullException("compiler");
            
            compilerPath = System.IO.Path.GetFullPath(compiler);
            comp = new Compiler()
            {
                CompilerExe = compilerPath
            };
        }

        public virtual bool IsSupported(IEnumerable<string> args)
        {
            cacheStart = DateTime.Now;
            if (FileUtils.Exists(compilerPath))
            {
                var rv = comp.ProcessArguments(args.ToArray());
                if (!rv)
                {
                    Logging.Emit("args not supported");
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

        public DataHash DeriveHashKey(IEnumerable<string> args)
        {
            var comphash = DigestCompiler(compilerPath);
            if (comphash.Result == DataHashResult.Ok)
            {
                var buf = new StringBuilder();
                buf.AppendLine(this.GetType().FullName.ToString());
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

        protected CacheManifest GetCachedManifestLocked(DataHash commonkey)
        {
            CacheManifest manifest = null;
            
            if (outputCache.ContainsEntry(commonkey.Hash, F_Manifest))
            {
                var mn = outputCache.MakePath(commonkey.Hash, F_Manifest);

                var m = jss.Deserialize<CacheManifest>(File.ReadAllText(mn));
                manifest = m;
            }

            return manifest;
        }

        async Task CopyOutputFilesAsync(DataHash hc)
        {
            await Task.Run(() =>
            {
                CopyOrHardLink(outputCache.MakePath(hc.Hash, F_Object), comp.ObjectTarget);
                if (comp.GeneratePdb)
                    CopyOrHardLink(outputCache.MakePath(hc.Hash, F_Pdb), comp.PdbFile);
            });
        }

        async Task CopyStdioAsync(DataHash hc)
        {
            await Task.Run(() =>
            {
                var stderrfile = outputCache.MakePath(hc.Hash, F_Stderr);
                var stdoutfile = outputCache.MakePath(hc.Hash, F_Stdout);

                OutputWrite(File.ReadAllText(stdoutfile));
                ErrorWrite(File.ReadAllText(stderrfile));
            });
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

        public virtual void CopyOrHardLink(string from, string to)
        {
            bool dolink = Settings.UseHardLinks;

            if (dolink)
            {
                if (Compiler.MakeHardLink(to, from))
                {
                    return;
                }
            }

            File.Copy(from, to, true);
        }

        protected int OnCacheHitLocked(DataHash hc, CacheManifest hm)
        {
            // we dont need the lock now, it is highly unlikley someone else will
            // modify these files
            outputCache.ReleaseMutex();

            var stdio = CopyStdioAsync(hc);
            var odata = CopyOutputFilesAsync(hc);

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
                        }
                    });
            });

            odata.Wait();
            stdio.Wait();
            tstat.Wait();

            return 0;
        }

        protected abstract bool CheckCache(IEnumerable<string> args, DataHash commonkey, out CacheManifest manifest);
        protected abstract int OnCacheMissLocked(DataHash hc, IEnumerable<string> args, CacheManifest m);

        protected int CompileWithStreams(IEnumerable<string> args, StreamWriter stderr, StreamWriter stdout, List<string> includes)
        {
            var rv = comp.InvokeCompiler(args,
                        x =>
                        {
                            ErrorWriteLine(x);
                            stderr.WriteLine(x);
                        }, y =>
                        {
                            OutputWriteLine(y);
                            stdout.WriteLine(y);
                        }, includes != null, includes);

            return rv;
        }

        public virtual int CompileOrCache(IEnumerable<string> args)
        {
            if (IsSupported(args))
            {
                var hc = DeriveHashKey(args);
                if (hc.Result == DataHashResult.Ok)
                {
                    CacheManifest hm;
                    if (CheckCache(args ,hc, out hm))
                    {
                        return OnCacheHitLocked(hc, hm);
                    }
                    else
                    {   // miss, try build
                        return OnCacheMissLocked(hc, args, hm);
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

            return CompileOnly(args);
        }

        public int CompileOnly(IEnumerable<string> args)
        {
            {
                return comp.InvokeCompiler(args, ErrorWriteLine, OutputWriteLine, false, null);
            }
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

        public virtual void Dispose()
        {
            if (Stats != null) Stats.Dispose();
            if (includeCache != null) includeCache.Dispose();
            if (outputCache != null) outputCache.Dispose();
        }

        public CacheStats Stats
        {
            get
            {
                return stats;
            }
        }


    }
}
