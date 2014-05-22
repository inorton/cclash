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
        public static StringBuilder MainStdErr = new StringBuilder();
        public static StringBuilder MainStdOut = new StringBuilder();

        public static int Main(string[] args) {
            var start = DateTime.Now;

            var dbg = Environment.GetEnvironmentVariable("CCLASH_DEBUG");
            if (!string.IsNullOrEmpty(dbg)) {
                Settings.DebugFile = dbg;
                Settings.DebugEnabled = true;
            }
            Compiler.FixEnv();

            var miss = Environment.GetEnvironmentVariable("CCLASH_MISSES");
            if (!string.IsNullOrEmpty(miss)) {
                Settings.MissLogFile = miss;
            }

            var aa = new CClashArgs();
            aa.Parse(args);


            if (Settings.DebugEnabled) {
                Logging.Emit("command line args:");
                foreach (var a in args) {
                    Logging.Emit("arg: {0}", a);
                }
            }

            if (aa.Matched) {
                if (aa.ServerMode != ServerMode.None) {

                    CClashServerBase server;

                    switch (aa.ServerMode) {
                        case ServerMode.Pipes:
                            server = new CClashPipeServer();
                            break;
                        case ServerMode.Inet:
                            server = new CClashInetServer();
                            break;
                        default:
                            throw new InvalidOperationException("unknown server mode");
                    }

                    Logging.Emit("server starting");
                    server.Listen(Settings.CacheDirectory);
                    return 0;
                }


                Logging.Emit("maint mode");
                Console.Error.WriteLine("cclash {0} (c) Ian Norton, April 2014",
                    typeof(Program).Assembly.GetName().Version.ToString());

                var compiler = Compiler.Find();

                if (Settings.ServiceMode) {
                    CClashServerClientBase cc = new CClashServerClientFactory().GetClient();
                    cc.Init(Settings.CacheDirectory);

                    if (aa.StopServer) {
                        cc.Transact(new CClashRequest() { cmd = Command.Quit });
                    } else {
                        Console.Out.WriteLine(cc.GetStats(compiler));
                    }
                } else {
                    Console.Out.WriteLine(StatOutputs.GetStatsString(compiler));
                }
                return 0;

            }

            var rv = RunBuild(args, start, AppendStdout, AppendStderr);
            if (rv != 0)
            {
                if (!Settings.NoAutoRebuild)
                {
                    for (int i = 1; i < 4; i++)
                    {
                        MainStdErr.Clear();
                        MainStdOut.Clear();
                        rv = RunBuild(args, start, AppendStdout, AppendStderr);
                        if (rv == 0) break;
                        Logging.Emit("compiler returned non-zero, doing auto-retry {0}", i);
                        System.Threading.Thread.Sleep(50);
                    }
                }
            }
            Console.Error.Write(MainStdErr.ToString());
            Console.Out.Write(MainStdOut.ToString());
            return rv;
        }

        static void AppendStderr(string str)
        {
            MainStdErr.AppendLine(str);
        }

        static void AppendStdout(string str)
        {
            MainStdOut.AppendLine(str);
        }

        private static int RunBuild(string[] args, DateTime start, Action<string> stdout, Action<string> stderr)
        {
            Logging.Emit("client mode = {0}", Settings.ServiceMode);
            try
            {
                if (!Settings.Disabled)
                {

                    string compiler = Compiler.Find();
                    if (compiler == null)
                        throw new System.IO.FileNotFoundException("cant find real cl compiler");

                    var cachedir = Settings.CacheDirectory;
                    Logging.Emit("compiler: {0}", compiler);

                    using (ICompilerCache cc =
                        CompilerCacheFactory.Get(Settings.DirectMode, cachedir))
                    {
                        var comp = cc.GetCompiler(compiler, Environment.CurrentDirectory, Compiler.GetEnvs());
                        return cc.CompileOrCache(comp, args, stderr, stdout);
                    }
                } else {
                    Logging.Emit("disabled by environment");
                }
            }
            catch (CClashWarningException e)
            {
                Logging.Warning(e.Message);
            }
            catch (Exception e)
            {

                Logging.Emit("{0} after {1} ms", e.GetType().Name, DateTime.Now.Subtract(start).TotalMilliseconds);
                Logging.Emit("{0} {1}", e.GetType().Name + " message: " + e.Message);
#if DEBUG
                Logging.Error("Exception from cacher {0}!!!", e);
#endif
            }

            int rv = -1;

            try
            {
                var c = new Compiler(Compiler.Find(), Environment.CurrentDirectory, Compiler.GetEnvs());
                rv = c.InvokeCompiler(args, stderr, stdout, false, null);
                Logging.Emit("exit {0} after {1} ms", rv, DateTime.Now.Subtract(start).TotalMilliseconds);
            }
            catch (CClashErrorException e)
            {
                Logging.Error(e.Message);
                throw;
            }
            catch (CClashWarningException e)
            {
                Logging.Warning(e.Message);
            }

            return rv;
        }
    }
}
