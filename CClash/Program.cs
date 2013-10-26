using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace CClash
{
    public class Program
    {

        public static string FindCompiler()
        {
            var self = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            var path = Environment.GetEnvironmentVariable("PATH");
            var paths = path.Split(';');

            var selfdir = Path.GetDirectoryName(self);
            var realcl = Path.Combine(selfdir, "cl_real.exe");
            if (File.Exists(realcl)) return realcl;

            foreach (var p in paths)
            {
                var f = Path.Combine(p, "cl.exe");
                if (FileUtils.Exists(f))
                {
                    if (f.Equals(self, StringComparison.CurrentCultureIgnoreCase))
                    {
                        continue;
                    }
                    if (Path.IsPathRooted(f))
                    {
                        
                    }

                    return f;
                }
            }

            return null;
        }

        public static int Main(string[] args)
        {
            var disable = Environment.GetEnvironmentVariable("CCLASH_DISABLE");
            var compiler = Environment.GetEnvironmentVariable("CCLASH_CL");

            var dbg = Environment.GetEnvironmentVariable("CCLASH_DEBUG");
            if (dbg != null)
            {
                Settings.DebugFile = dbg;
                Settings.DebugEnabled = true;
            }

            var cachedir = Environment.GetEnvironmentVariable("CCLASH_DIR");

            if (string.IsNullOrEmpty(cachedir))
            {
                var appdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                cachedir = Path.Combine(appdata, "clcache-data");
            }

            Logging.Emit("cache folder: {0}", cachedir);

            if (compiler == null) compiler = FindCompiler();
            if (compiler == null) throw new System.IO.FileNotFoundException("cant find real cl compiler");

            Logging.Emit("compiler: {0}", compiler);

            if (Settings.DebugEnabled)
            {
                Logging.Emit("command line args:");
                foreach (var a in args)
                {
                    Logging.Emit("arg: {0}", a);
                }
            }

            if (args.Contains("--cclash"))
            {
                Console.WriteLine("compiler: {0}", compiler);
                Console.WriteLine("cachedir: {0}", cachedir);
                if (string.IsNullOrEmpty(disable))
                {
                    Console.WriteLine("disabled: no");
                }
                else
                {
                    Console.WriteLine("disabled: yes");
                }
                if (compiler != null)
                {
                    var cache = new CompilerCache(cachedir, compiler);
                    Console.WriteLine("outputCache usage: {0} kb", (int)(cache.CacheSize / 1024));
                    Console.WriteLine("cached files: {0}", cache.CacheObjects);
                    Console.WriteLine("hits: {0}", cache.CacheHits);
                    Console.WriteLine("misses: {0}", cache.CacheMisses);
                    Console.WriteLine("unsupported: {0}", cache.CacheUnsupported);
                }
                return 0;
            }


            var clc = new CompilerCache(cachedir, compiler);
            if (string.IsNullOrEmpty(disable))
            {
                return clc.CompileOrCache(args);
            }
            else
            {
                Logging.Emit("cclash disabled");
            }
            return clc.CompileOnly(args);

        }
    }
}
