using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace CClash
{
    public sealed class Program
    {

        public static int Main(string[] args)
        {
            var start = DateTime.Now;
            try
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

                if (args.Contains("--cclash-server"))
                {
                    var server = new CClashServer();
                    server.Listen(Settings.CacheDirectory);
                    return 0;
                }
                
                if (args.Contains("--cclash"))
                {
                    Logging.Emit("maint mode");
                    Console.Error.WriteLine("cclash {0} (c) Ian Norton, November 2013", 
                        typeof(Program).Assembly.GetName().Version.ToString());

                    var compiler = Compiler.Find();
                    if (Settings.ServiceMode)
                    {
                        var cc = new CClashServerClient(Settings.CacheDirectory);

                        if (args.Contains("--stop"))
                        {
                            cc.Transact(new CClashRequest() { cmd = Command.Quit });
                        }
                        else
                        {
                            Console.Out.WriteLine(cc.GetStats(compiler));
                        }
                    }
                    else
                    {
                        Console.Out.WriteLine(StatOutputs.GetStatsString(compiler));
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

                    using ( ICompilerCache cc = CompilerCacheFactory.Get( Settings.DirectMode, cachedir, compiler ) )
                    {
                        cc.SetCompiler(compiler);
                        return cc.CompileOrCache(args);
                    }
                }
                else
                {
                    Logging.Emit("disabled by environment");
                }

            }
            catch (Exception e)
            {
                Logging.Emit("{0} after {1} ms", e.GetType().Name, DateTime.Now.Subtract(start).TotalMilliseconds);
                Logging.Emit("{0} {1}",e.GetType().Name + " message: " + e.Message);
#if DEBUG
                Logging.Error("Exception from cacher {0}!!!", e);
#endif
            }

            var rv = new Compiler()
            {
                CompilerExe = Compiler.Find(),
            }.InvokeCompiler(args, Console.Error.WriteLine, Console.Out.WriteLine, false, null);
            Logging.Emit("exit {0} after {1} ms", rv, DateTime.Now.Subtract(start).TotalMilliseconds);
            return rv;
        }
    }
}
