using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Pipes;
using System.Web.Script.Serialization;
using System.Diagnostics;
using System.Collections.Specialized;

namespace CClash
{
    public sealed class CClashServerClientCompiler : ICompiler
    {
        public string CompilerExe { get; set; }
        public bool ProcessArguments(string[] args)
        {
            return true;
        }

        public List<string> CliIncludePaths
        {
            get { throw new NotImplementedException(); }
        }

        public string[] CommandLine
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public Dictionary<string, string> Envs
        {
            get { throw new NotImplementedException(); }
        }

        public IEnumerable<string> FixupArgs(IEnumerable<string> args)
        {
            throw new NotImplementedException();
        }

        public bool GeneratePdb
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public string GetPath(string path)
        {
            throw new NotImplementedException();
        }

        public List<string> GetPotentialIncludeFiles(IEnumerable<string> incdirs, IEnumerable<string> incfiles)
        {
            throw new NotImplementedException();
        }

        public List<string> GetUsedIncludeDirs(List<string> files)
        {
            throw new NotImplementedException();
        }

        public int InvokeCompiler(IEnumerable<string> args, Action<string> onStdErr, Action<string> onStdOut, bool showIncludes, List<string> foundIncludes)
        {
            throw new NotImplementedException();
        }

        public int InvokePreprocessor(StreamWriter stdout)
        {
            throw new NotImplementedException();
        }

        public bool Linking
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public string ObjectTarget
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public string PdbFile
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public bool PrecompiledHeaders
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public string ResponseFile
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public bool SingleSource
        {
            get { throw new NotImplementedException(); }
        }

        public string SingleSourceFile
        {
            get { throw new NotImplementedException(); }
        }

        public string[] SourceFiles
        {
            get { throw new NotImplementedException(); }
        }

        public string WorkingDirectory
        {
            get { throw new NotImplementedException(); }
        }
    }

    public sealed class CClashServerClient : ICompilerCache
    {
        NamedPipeClientStream ncs;
        string pipename = null;
        bool spawnServer = true;

        Process serverProcess = null;

        public CClashServerClient(string cachedir)
        {
            pipename = CClashServer.MakePipeName(cachedir);
        }

        public CClashServerClient(string cachedir, bool startServer) : this(cachedir)
        {
            spawnServer = startServer;
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

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    if (!ncs.IsConnected)
                        ncs.Connect(50);
                    ncs.ReadMode = PipeTransmissionMode.Message;
                    return;
                }
                catch (IOException)
                {
                    try { ncs.Dispose(); Open(); }
                    catch { }
                }
                catch (TimeoutException)
                {
                    if (spawnServer)
                    {
                        Logging.Emit("starting background service");
                        serverProcess = new Process();
                        var psi = new ProcessStartInfo(exe);
                        psi.CreateNoWindow = true;
                        psi.Arguments = "--cclash-server";
                        psi.ErrorDialog = false;
                        psi.WindowStyle = ProcessWindowStyle.Hidden;
                        serverProcess.StartInfo = psi;
                        serverProcess.Start();
                        serverProcess.Exited += (o,a) =>
                        {
                            Logging.Emit("server exited with status {0}", serverProcess.ExitCode);
                        };
                        System.Threading.Thread.Sleep(500);
                    }
                    else
                    {
                        throw new DirectoryNotFoundException("cclash server");
                    }
                }
            }
            throw new InvalidProgramException("server failed to start");
        }

        public ICacheInfo Stats
        {
            get
            {
                return null;
            }
        }

        public bool IsSupported( ICompiler comp, IEnumerable<string> args)
        {
            return true;
        }

        string compilerPath;
        string workingDirectory;
        Dictionary<string, string> environment = new Dictionary<string, string>();
        public ICompiler GetCompiler(string compiler, string workdir, IDictionary<string,string> envs)
        {
            if (string.IsNullOrEmpty(compiler)) throw new ArgumentNullException("compiler");
            workingDirectory = workdir;
            compilerPath = System.IO.Path.GetFullPath(compiler);
            foreach (string n in envs.Keys)
                environment[n] = envs[n];
            return new CClashServerClientCompiler() { CompilerExe = compiler };
        }

        public StringBuilder GetStdioStringBuilder()
        {
            return null;
        }

        public int CompileOrCache(ICompiler comp, IEnumerable<string> args, Action<string> stderr, Action<string> stdout)
        {
            var req = new CClashRequest()
            {
                cmd = Command.Run,
                compiler = compilerPath,
                envs = environment,
                workdir = workingDirectory,
                argv = new List<string> ( args ),
            };
            var resp = Transact(req);
            if (resp != null)
            {
                if (stderr != null) stderr(resp.stderr);
                if (stdout != null) stdout(resp.stdout);

                return resp.exitcode;
            }

            return -1;
            
        }

        public CClashResponse Transact(CClashRequest req)
        {
            Connect();
            CClashResponse resp = null;

            var txbuf = req.Serialize();
            ncs.Write(new byte[] { txbuf.Length }, 0, 1);
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

        public DataHash DeriveHashKey( ICompiler unused, IEnumerable<string> args)
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
                if ( serverProcess != null ) serverProcess.Dispose();
                ncs.Dispose();
            }
        }
    }
}
