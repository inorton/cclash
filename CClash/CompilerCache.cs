using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Web.Script.Serialization;

namespace CClash
{
    public class CompilerCache
    {
        JavaScriptSerializer jss = new JavaScriptSerializer();
        FileCacheStore cache;
        String compilerPath;
        HashUtil hasher = new HashUtil();
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

        long ReadStat(string statfile)
        {
            try
            {
                var x = File.ReadAllText(cache.MakePath(K_Stats, statfile));
                return Int64.Parse(x);
            }
            catch
            {
                return 0;
            }
        }

        void WriteStat(string statfile, long value)
        {
            cache.AddEntry(K_Stats);
            File.WriteAllText( cache.MakePath(K_Stats, statfile), value.ToString());
        }

        long CacheHits
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

        long CacheSize
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

        long CacheMisses
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

        long CacheUnsupported
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

        long CacheObjects
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

        public CompilerCache(string cacheFolder, string compiler)
        {
            if (string.IsNullOrEmpty(cacheFolder)) throw new ArgumentNullException("cacheFolder");
            if (string.IsNullOrEmpty(compiler)) throw new ArgumentNullException("compiler");
            cache = FileCacheStore.Load(cacheFolder);
            compilerPath = System.IO.Path.GetFullPath(compiler);
            comp = new Compiler() {
                CompilerExe = compilerPath 
            };
        }

        public bool IsSupported(IEnumerable<string> args)
        {
            if (File.Exists(compilerPath))
            {
                return comp.ProcessArguments(args.ToArray());
            }
            throw new FileNotFoundException(compilerPath);
        }

        public DataHash DeriveHashKey( IEnumerable<string> args )
        {
            var comphash = hasher.DigestFile(compilerPath);
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

        public bool CheckCache(DataHash commonkey)
        {
            if (cache.ContainsEntry(commonkey.Hash, F_Manifest))
            {
                var mn = cache.MakePath(commonkey.Hash, F_Manifest);
                
                var m = jss.Deserialize<CacheManifest>(File.ReadAllText(mn));
                foreach ( var f in m.PotentialNewIncludes ) {
                    if (File.Exists(f)) return false;
                }
                foreach (var ent in m.IncludeFiles)
                {
                    var h = hasher.DigestFile(ent.Key);
                    if (h.Result == DataHashResult.Ok)
                    {
                        if (h.Hash != ent.Value) return false;
                    } else
                    {
                        return false;
                    }
                }
                foreach (var f in new string[] { F_Manifest, F_Object, F_Stderr, F_Stdout })
                    if (!File.Exists(cache.MakePath(commonkey.Hash, f)))
                    {
                        cache.Remove(commonkey.Hash);
                        return false;
                    }

                return true; // cache hit, all includes match and no new files added
            }
            return false;
        }

        public int CompileOrCache(IEnumerable<string> args)
        {
            if (IsSupported(args))
            {
                var hc = DeriveHashKey(args);
                if (hc.Result == DataHashResult.Ok)
                {
                    cache.WaitOne();
                    try
                    {
                        cache.AddEntry(hc.Hash);
                        var stderrfile = cache.MakePath(hc.Hash, F_Stderr);
                        var stdoutfile = cache.MakePath(hc.Hash, F_Stdout);
                        if (CheckCache(hc))
                        {
                            CacheHits++;
                            // cache hit
                            Console.Out.Write(File.ReadAllText(stdoutfile));
                            Console.Error.Write(File.ReadAllText(stderrfile));
                            File.Copy(cache.MakePath(hc.Hash, F_Object), comp.ObjectTarget, true);
                            if (comp.GeneratePdb)
                                File.Copy(cache.MakePath(hc.Hash, F_Pdb), comp.PdbFile, true);
                            return 0;
                        }
                        else
                        {   // miss, try build
                            CacheMisses++;
                            Console.Error.WriteLine("cache miss");
                            using (var stderrfs = new StreamWriter(stderrfile))
                            {
                                using (var stdoutfs = new StreamWriter(stdoutfile))
                                {
                                    var rv = comp.InvokeCompiler(args,
                                        x =>
                                        {
                                            Console.Error.WriteLine(x);
                                            stderrfs.WriteLine(x);
                                        }, y =>
                                        {
                                            Console.Out.WriteLine(y);
                                            stdoutfs.WriteLine(y);
                                        }, false);

                                    if (rv == 0)
                                    {
                                        // run preprocessor
                                        var ifiles = new List<string>();
                                        var idirs = new List<string>();
                                        var irv = comp.GetUsedIncludeFiles(args, ifiles, idirs);
                                        if (irv == 0)
                                        {
                                            // save manifest and other things to cache
                                            var others = comp.GetPotentialIncludeFiles(idirs, ifiles);
                                            var m = new CacheManifest();
                                            m.PotentialNewIncludes = others;
                                            m.IncludeFiles = new Dictionary<string, string>();
                                            m.TimeStamp = DateTime.Now.ToString("s");
                                            m.CommonHash = hc.Hash;

                                            bool good = true;

                                            foreach ( var f in ifiles ) {
                                                var h = hasher.DigestFile(f);
                                                if (h.Result == DataHashResult.Ok)
                                                {
                                                    m.IncludeFiles[f] = h.Hash;
                                                }
                                                else
                                                {
                                                    good = false;
                                                    break;
                                                }
                                            }

                                            if (good)
                                            {
                                                cache.AddFile(hc.Hash, comp.ObjectTarget, F_Object);
                                                CacheObjects++;
                                                CacheSize += new FileInfo(comp.ObjectTarget).Length;
                                                if (comp.GeneratePdb)
                                                {
                                                    cache.AddFile(hc.Hash, comp.PdbFile, F_Pdb);
                                                    CacheSize += new FileInfo(comp.PdbFile).Length;
                                                }
                                                // write manifest
                                                var mt = jss.Serialize(m);
                                                cache.AddTextFileContent( hc.Hash, F_Manifest, mt );
                                                CacheSize += mt.Length;
                                            }
                                        }
                                    }

                                    return rv;
                                }
                            }
                        }
                        
                    }
                    finally
                    {
                        cache.ReleaseMutex();
                    }
                }
            }
            
            return CompileOnly(args);
        }

        public int CompileOnly(IEnumerable<string> args)
        {
            cache.WaitOne();
            try
            {
                CacheUnsupported++;
                return comp.InvokeCompiler(args, Console.Error.WriteLine, Console.Out.WriteLine, false);
            }
            finally
            {
                cache.ReleaseMutex();
            }
        }
    }
}
