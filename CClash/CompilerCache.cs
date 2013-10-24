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
            outputCache = FileCacheStore.Load( Path.Combine(cacheFolder, "outputs" ) );
            includeCache = FileCacheStore.Load(Path.Combine(cacheFolder, "includes"));
            hasher = new HashUtil(includeCache);
            compilerPath = System.IO.Path.GetFullPath(compiler);
            comp = new Compiler() {
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

        public bool CheckCache(DataHash commonkey)
        {
            if (outputCache.ContainsEntry(commonkey.Hash, F_Manifest))
            {
                var mn = outputCache.MakePath(commonkey.Hash, F_Manifest);
                
                var m = jss.Deserialize<CacheManifest>(File.ReadAllText(mn));
                foreach ( var f in m.PotentialNewIncludes ) {
                    if (FileUtils.Exists(f)) return false;
                }
                var hashes = hasher.DigestFiles(m.IncludeFiles.Keys);

                foreach (var h in hashes)
                {
                    if (h.Value.Result == DataHashResult.Ok)
                    {
                        if (m.IncludeFiles[h.Key] != h.Value.Hash) return false;
                    }
                    else
                    {
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

        public int CompileOrCache(IEnumerable<string> args)
        {
            if (IsSupported(args))
            {
                var hc = DeriveHashKey(args);
                if (hc.Result == DataHashResult.Ok)
                {
                    outputCache.WaitOne();
                    try
                    {
                        outputCache.AddEntry(hc.Hash);
                        var stderrfile = outputCache.MakePath(hc.Hash, F_Stderr);
                        var stdoutfile = outputCache.MakePath(hc.Hash, F_Stdout);
                        if (CheckCache(hc))
                        {
                            CacheHits++;
                            // cache hit
                            Console.Out.Write(File.ReadAllText(stdoutfile));
                            Console.Error.Write(File.ReadAllText(stderrfile));
                            File.Copy(outputCache.MakePath(hc.Hash, F_Object), comp.ObjectTarget, true);
                            if (comp.GeneratePdb)
                                File.Copy(outputCache.MakePath(hc.Hash, F_Pdb), comp.PdbFile, true);
                            return 0;
                        }
                        else
                        {   // miss, try build
                            CacheMisses++;
                            using (var stderrfs = new StreamWriter(stderrfile))
                            {
                                var ifiles = new List<string>();
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
                                        }, true, ifiles);

                                    if (rv == 0)
                                    {
                                        // run preprocessor
                                        
                                        var idirs =comp.GetUsedIncludeDirs(ifiles);
                                        if (idirs.Count > 0)
                                        {
                                            // save manifest and other things to cache
                                            var others = comp.GetPotentialIncludeFiles(idirs, ifiles);
                                            var m = new CacheManifest();
                                            m.PotentialNewIncludes = others;
                                            m.IncludeFiles = new Dictionary<string, string>();
                                            m.TimeStamp = DateTime.Now.ToString("s");
                                            m.CommonHash = hc.Hash;

                                            bool good = true;

                                            var hashes = hasher.DigestFiles(ifiles);

                                            foreach (var x in hashes)
                                            {
                                                if (x.Value.Result == DataHashResult.Ok)
                                                {
                                                    m.IncludeFiles[x.Key] = x.Value.Hash;
                                                }
                                                else
                                                {
                                                    good = false;
                                                    break;
                                                }
                                            }

                                            if (good)
                                            {
                                                outputCache.AddFile(hc.Hash, comp.ObjectTarget, F_Object);
                                                CacheObjects++;
                                                CacheSize += new FileInfo(comp.ObjectTarget).Length;
                                                if (comp.GeneratePdb)
                                                {
                                                    outputCache.AddFile(hc.Hash, comp.PdbFile, F_Pdb);
                                                    CacheSize += new FileInfo(comp.PdbFile).Length;
                                                }
                                                // write manifest
                                                var mt = jss.Serialize(m);
                                                outputCache.AddTextFileContent( hc.Hash, F_Manifest, mt );
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
                        outputCache.ReleaseMutex();
                    }
                }
            }
            
            return CompileOnly(args);
        }

        public int CompileOnly(IEnumerable<string> args)
        {
            outputCache.WaitOne();
            try
            {
                CacheUnsupported++;
                return comp.InvokeCompiler(args, Console.Error.WriteLine, Console.Out.WriteLine, false, null);
            }
            finally
            {
                outputCache.ReleaseMutex();
            }
        }
    }
}
