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
        public static int Main(string[] args)
        {
            var dbg = Environment.GetEnvironmentVariable("CCLASH_DEBUG");
            if (!string.IsNullOrEmpty(dbg))
            {
                Settings.DebugFile = dbg;
                Settings.DebugEnabled = true;
            }


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
                var compiler = Compiler.Find();
                Console.WriteLine("compiler: {0}", compiler);
                Console.WriteLine("cachedir: {0}", Settings.CacheDirectory);
                if (Settings.DebugEnabled)
                {
                    Console.WriteLine("debug file: {0}", Settings.DebugFile);
                }
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
                    var cache = new CompilerCache(Settings.CacheDirectory, compiler);
                    Console.WriteLine("outputCache usage: {0} kb", (int)(cache.CacheSize / 1024));
                    Console.WriteLine("cached files: {0}", cache.CacheObjects);
                    Console.WriteLine("hits: {0}", cache.CacheHits);
                    Console.WriteLine("misses: {0}", cache.CacheMisses);
                    Console.WriteLine("unsupported: {0}", cache.CacheUnsupported);
                    Console.WriteLine("slow hits: {0}", cache.SlowHitCount);
                    Console.WriteLine("time lost: {0} mins", Math.Round( cache.SecondsWasted / 60.0 ));
                    Console.WriteLine("time saved: {0} mins", Math.Round( cache.SecondsSaved / 60.0 ));
                }
                return 0;
            }

            if (!Settings.Disabled)
            {
                string compiler = Compiler.Find();
                if (compiler == null)
                    throw new System.IO.FileNotFoundException("cant find real cl compiler");

                var cachedir = Settings.CacheDirectory;
                Logging.Emit("compiler: {0}", compiler);
                return new CompilerCache(cachedir, compiler).CompileOrCache(args);
            }

            return new Compiler()
            {
                CompilerExe = Compiler.Find(),
            }.InvokeCompiler(args, Console.Error.WriteLine, Console.Out.WriteLine, false, null);
        }
    }
}
