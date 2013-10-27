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
            var compiler = Environment.GetEnvironmentVariable("CCLASH_CL");

            var dbg = Environment.GetEnvironmentVariable("CCLASH_DEBUG");
            if (dbg != null)
            {
                Settings.DebugFile = dbg;
                Settings.DebugEnabled = true;
            }

            var cachedir = Settings.CacheDirectory;

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
                if (Settings.Disabled)
                {
                    Console.WriteLine("disabled: yes");
                }
                else
                {
                    Console.WriteLine("disabled: no");
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

            if (!Settings.Disabled)
            {
                return new CompilerCache(cachedir, compiler).CompileOrCache(args);
            }
            else
            {
                Logging.Emit("cclash disabled");
            }

            var c = new Compiler()
            {
                CompilerExe = compiler,
            };

            return c.InvokeCompiler(args,
                Console.Error.WriteLine, Console.Out.WriteLine, false, null);
        }
    }
}
