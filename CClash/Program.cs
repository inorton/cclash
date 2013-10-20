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
            foreach (var p in paths)
            {
                var f = Path.Combine(p, "cl.exe");
                if (File.Exists(f))
                {
                    if (f.Equals(self, StringComparison.CurrentCultureIgnoreCase))
                    {
                        continue;
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

            var cachedir = Environment.GetEnvironmentVariable("CCLASH_DIR");

            
            if (string.IsNullOrEmpty(cachedir))
            {
                var appdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                cachedir = Path.Combine(appdata, "clcache-data");
            }

            if (compiler == null) compiler = FindCompiler();
            if (compiler == null) throw new System.IO.FileNotFoundException("cant find real cl compiler");

            if (args.Contains("--cclash"))
            {
                Console.Error.WriteLine("compiler = {0}", compiler);
                Console.Error.WriteLine("cachedir = {0}", cachedir);
                if (string.IsNullOrEmpty(disable))
                {
                    Console.Error.WriteLine("disabled = yes");
                }
                else
                {
                    Console.Error.WriteLine("disabled = no");
                }
            }


            var clc = new CompilerCache(cachedir, compiler);
            if (string.IsNullOrEmpty(disable)) 
                return clc.CompileOrCache(args);
            return clc.CompileOnly(args);

        }
    }
}
