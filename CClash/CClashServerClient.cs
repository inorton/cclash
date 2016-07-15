using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Pipes;
using System.Web.Script.Serialization;
using System.Diagnostics;

namespace CClash
{
    public sealed class CClashServerClient : ICompilerCache
    {
        NamedPipeClientStream ncs;
        string pipename = null;

        public CClashServerClient(string cachedir)
        {
            pipename = CClashServer.MakePipeName(cachedir);
        }

        public IFileCacheStore OutputCache
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        void Open()
        {      
            ncs = new NamedPipeClientStream(".", pipename, PipeDirection.InOut);
        }

        void Connect()
        {
            var exe = GetType().Assembly.Location;
            if (ncs == null)
                Open();

            try
            {
                ConnectClient();
                return;
            }
            catch (IOException ex)
            {
                Logging.Emit("error connecting {0}", ex.Message);
                try { ncs.Dispose(); Open(); }
                catch { }
            }
            catch (TimeoutException)
            {
                Logging.Emit("could not connect to cclash service");
                throw new CClashServerNotReadyException();
            }
        }

        public static void StartBackgroundServer() {
            using (var ssm = new System.Threading.Mutex(false, "cclash_server_spawn")) {
                var can_start_server = ssm.WaitOne(500);
                try {
                    if (can_start_server) {
                        Logging.Emit("starting new server");
                        // start the server
                        var p = new Process();
                        var ours = FileUtils.GetShortPath( typeof(CClashServerClient).Assembly.Location);
                        var exedir = Path.GetDirectoryName(ours);
                        var exepath = Path.Combine(exedir, "cclash.exe");
                        if (!File.Exists(exepath)) {
                            exepath = ours;
                        }
                        var pargs = new List<string>
                                {
                                    exepath,
                                    "--cclash-server"
                                };
                        if (Settings.DebugFile != null) {
                            pargs.Add("--debug");
                        }

                        var command = "cmd";
                        var command_args = "/c " + string.Join(" ", pargs.ToArray());

                        p.StartInfo = new ProcessStartInfo(command);
                        p.StartInfo.UseShellExecute = false;
                        p.StartInfo.CreateNoWindow = true;
                        p.StartInfo.Arguments = command_args;
                        p.StartInfo.ErrorDialog = false;
                        p.StartInfo.WorkingDirectory = Path.GetPathRoot(Environment.CurrentDirectory);
                        p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        p.Start();
                    }
                    System.Threading.Thread.Sleep(1000);
                } finally {
                    if (can_start_server) {
                        ssm.ReleaseMutex();
                    }
                }
            }
        }

        private void ConnectClient()
        {
            if (!ncs.IsConnected)
                ncs.Connect(500);
            ncs.ReadMode = PipeTransmissionMode.Message;
        }

        public ICacheInfo Stats
        {
            get
            {
                return null;
            }
        }

        public bool IsSupported(ICompiler comp, IEnumerable<string> args)
        {
            return true;
        }

        string compilerPath;
        string workingdir;
        Dictionary<string, string> environment;

        public bool CheckCache(ICompiler comp, IEnumerable<string> args, DataHash commonkey, out CacheManifest manifest)
        {
            manifest = null;
            return false;
        }

        public ICompiler SetCompiler(string compiler, string workdir, System.Collections.Generic.Dictionary<string,string> envs )
        {
            if (string.IsNullOrEmpty(compiler)) throw new ArgumentNullException("compiler");
            if (string.IsNullOrEmpty(workdir)) throw new ArgumentNullException("workdir");
            environment = envs;
            workingdir = workdir;
            compilerPath = System.IO.Path.GetFullPath(compiler);
            try
            {
                Connect();
            }
            catch (CClashServerNotReadyException)
            {
                var c = new Compiler() { CompilerExe = compiler };
                c.SetWorkingDirectory(workdir);
                c.SetEnvironment(envs);
                return c;
            }
            return null;
        }

        Action<string> stdOutCallback = null;
        Action<string> stdErrCallback = null;
        public void SetCaptureCallback(ICompiler comp, Action<string> onOutput, Action<string> onError)
        {
            stdOutCallback = onOutput;
            stdErrCallback = onError;
        }

        public int CompileOrCache(ICompiler comp, IEnumerable<string> args, CClashRequest unused)
        {
            Logging.Emit("client args: {0}", string.Join(" ", args.ToArray()));

            if (comp != null) // compiler is set, server wasnt ready
            {
                return comp.InvokeCompiler(args, null, null, false, new List<string>());
            }

            try {
                var req = new CClashRequest()
                {
                    cmd = Command.Run,
                    compiler = compilerPath,
                    envs = environment,
                    workdir = workingdir,
                    argv = new List<string> ( args ),
                };
                var resp = Transact(req);
                if (resp != null)
                {
                    if (stdErrCallback != null)
                    {
                        stdErrCallback(resp.stderr);
                    }
                    else
                    {
                        Console.Error.Write(resp.stderr);
                    }
                    if (stdOutCallback != null)
                    {
                        stdOutCallback(resp.stdout);
                    }
                    else
                    {
                        Console.Out.Write(resp.stdout);
                    }

                    return resp.exitcode;
                }
                else
                {
                    throw new CClashErrorException("server returned no response");
                }
            } catch (Exception e) {
                Logging.Emit("server error! {0}", e);
                throw new CClashWarningException("server error");
            }
        }

        public CClashResponse Transact(CClashRequest req)
        {
            Connect();
            CClashResponse resp = null;
            req.pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            var txbuf = req.Serialize();

            ncs.Write(txbuf, 0, txbuf.Length);
            ncs.Flush();

            var rx = new List<byte>();

            var rxbuf = new byte[8192];
            do
            {
                var rbytes = ncs.Read(rxbuf, 0, rxbuf.Length);
                rx.AddRange(rxbuf.Take(rbytes));
            } while (!ncs.IsMessageComplete);

            if (rx.Count > 0)
            {
                resp = CClashMessage.Deserialize<CClashResponse>(rx.ToArray());
                ncs.Close();
            }
            return resp;
        }

        public DataHash DeriveHashKey(ICompiler comp, IEnumerable<string> args)
        {
            throw new NotSupportedException();
        }

        public string GetStats(string compiler)
        {
            var req = new CClashRequest()
            {
                cmd = Command.GetStats,
                compiler = compiler,
            };

            var resp = Transact(req);
            return resp.stdout;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                ncs.Dispose();
            }
        }
    }
}
