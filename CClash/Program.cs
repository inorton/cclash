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

        public static CClashServer Server = null;

        public static int Main(string[] args)
        {
            var start = DateTime.Now;

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
                
                if (args.Contains("--attempt-pdb"))
                {
                    Environment.SetEnvironmentVariable("CCLASH_ATTEMPT_PDB_CACHE", "yes");
                }
                if (args.Contains("--pdb-to-z7"))
                {
                    Environment.SetEnvironmentVariable("CCLASH_Z7_OBJ", "yes");
                }

                if (Settings.DebugEnabled)
                {
                    if (Settings.DebugFile != null)
                    {
                        Settings.DebugFile += ".serv";
                    }
                }

                if (args.Contains("--debug")) {
                    if (Settings.DebugFile == null) {
                        Settings.DebugFile = "Console";
                        Settings.DebugEnabled = true;
                    }
                }

                Logging.Emit("starting in server mode");

                if (Settings.DebugFile != "Console") {
                    Logging.Emit("closing server console");
                    Console.Out.Close();
                    Console.Error.Close();
                    Console.In.Close();
                }

                
                Server = new CClashServer();
                if (Server.Preflight(Settings.CacheDirectory))
                {
                    Logging.Emit("server created");
                    Server.Listen(Settings.CacheDirectory);
                    return 0;
                }
                else
                {
                    Logging.Emit("another server is running.. quitting");
                    return 1;
                }
            }

            if (args.Contains("--cclash"))
            {
                Logging.Emit("maint mode");
                Console.Error.WriteLine("cclash {0} (c) Ian Norton, April 2016",
                    typeof(Program).Assembly.GetName().Version.ToString());

                var compiler = Compiler.Find();
                if (Settings.ServiceMode)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            var cc = new CClashServerClient(Settings.CacheDirectory);
                            if (args.Contains("--stop"))
                            {
                                cc.Transact(new CClashRequest() { cmd = Command.Quit });
                            }
                            else {
                                #region server commands
                                if (args.Contains("--clear")) {
                                    cc.Transact(new CClashRequest() { cmd = Command.ClearCache });
                                } else if ( args.Contains("--disable") ){
                                    cc.Transact(new CClashRequest() { cmd = Command.DisableCache });
                                } else if (args.Contains("--enable") ){
                                    cc.Transact(new CClashRequest() { cmd = Command.EnableCache });
                                } else if (args.Contains("--start")) {
                                    Console.Out.WriteLine("starting server");
                                    CClashServerClient.StartBackgroundServer();
                                } else {
                                    Console.Out.WriteLine(cc.GetStats(compiler));
                                }
                                return 0;

                                #endregion
                            }

                        }
                        catch (CClashErrorException ex)
                        {
                            Logging.Error(ex.Message);
                            return -1;
                        }
                        catch (CClashWarningException)
                        {
                            System.Threading.Thread.Sleep(2000);
                        }
                        catch (CClashServerNotReadyException)
                        {
                            Logging.Emit("server not ready, try again");
                            return -1;
                        }
                        catch (IOException ex)
                        {
                            Logging.Error(ex.ToString());
                            return -1;
                        }
                    }
                }
                else
                {
                    ICompiler comp;
                    using (ICompilerCache cc =
                        CompilerCacheFactory.Get(Settings.DirectMode, Settings.CacheDirectory, compiler, Environment.CurrentDirectory, Compiler.GetEnvironmentDictionary(), out comp))
                    {
                        Console.Out.WriteLine(StatOutputs.GetStatsString(compiler, cc));
                    }
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
                        System.Threading.Thread.Sleep(100);
                    }
                }
            }
            Console.Error.Write(MainStdErr.ToString());
            Console.Out.Write(MainStdOut.ToString());

            if (spawnServer) {
                Logging.Emit("server needs to be started");
            }
            return rv;
        }

        static void AppendStderr(string str)
        {
            MainStdErr.Append(str);
        }

        static void AppendStdout(string str)
        {
            MainStdOut.Append(str);
        }

        static bool spawnServer = false;

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
                    ICompiler comp;
                    using (ICompilerCache cc =
                        CompilerCacheFactory.Get(Settings.DirectMode, cachedir, compiler, Environment.CurrentDirectory, Compiler.GetEnvironmentDictionary(), out comp))
                    {
                        if (comp != null) spawnServer = true;
                        cc.SetCaptureCallback(comp, stdout, stderr);
                        return cc.CompileOrCache(comp, args);
                    }
                }
                else
                {
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
               
                var c = new Compiler()
                {
                    CompilerExe = Compiler.Find(),
                };
                c.SetEnvironment(Compiler.GetEnvironmentDictionary());
                c.SetWorkingDirectory(Environment.CurrentDirectory);
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
