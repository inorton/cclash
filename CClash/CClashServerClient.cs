using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Pipes;
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

    public abstract class CClashServerClientBase
    {
        public Process ServerProcess { get; set; }

        public bool SpawnServer { get; set; }

        protected void StartServer()
        {
            if (SpawnServer)
            {
                var exe = GetType().Assembly.Location;
                Logging.Emit("starting background service");
                ServerProcess = new Process();
                var psi = new ProcessStartInfo(exe);
                psi.CreateNoWindow = true;
                psi.Arguments = "--cclash-server";
                psi.ErrorDialog = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                ServerProcess.StartInfo = psi;
                ServerProcess.Start();
                ServerProcess.Exited += (o, a) =>
                {
                    Logging.Emit("server exited with status {0}", ServerProcess.ExitCode);
                };
                System.Threading.Thread.Sleep(500);
            }
        }

        public abstract void Connect();
        public abstract void Disconnect();

        public Stream Stream
        {
            get;
            protected set;
        }

        public StreamReader GetReader()
        {
            if (Stream != null)
            {
                return new StreamReader(Stream);
            }
            throw new InvalidOperationException("no stream set");
        }

        public StreamWriter GetWriter()
        {
            if (Stream != null)
            {
                return new StreamWriter(Stream);
            }
            throw new InvalidOperationException("no stream set");
        }

        public CClashResponse Transact(CClashRequest req)
        {
            Connect();
            CClashResponse resp = new CClashResponse();

            req.SerializeMessage(Stream);

            var rxbuf = new StringBuilder();
            string line = string.Empty;

            var sr = GetReader();

            do
            {
                line = sr.ReadLine();
                rxbuf.AppendLine(line);
                if (line == null) break;
                if (line == ":end:") break;
            } while (true);

            if (rxbuf.Length > 0)
            {
                resp.Deserialize(rxbuf.ToString());
            }
            return resp;
        }
    }

    public sealed class CClashServerClient : CClashServerClientBase, ICompilerCache
    {
        NamedPipeClientStream ncs;
        string pipename = null;
        bool spawnServer = true;

        Process serverProcess = null;

        public CClashServerClient(string cachedir)
        {
            pipename = CClashPipeServer.MakePipeName(cachedir);
        }

        public CClashServerClient(string cachedir, bool startServer) : this(cachedir)
        {
            spawnServer = startServer;
        }

        NamedPipeClientStream OpenPipe()
        {
            return new NamedPipeClientStream(".", pipename, PipeDirection.InOut);
        }

        public override void Disconnect()
        {
            if (Stream != null)
            {
                Stream.Close();
            }
            Stream = null;
        }

        public override void Connect()
        {
            var ncs = OpenPipe();
            int i;
            for (i = 0; i < 10; i++)
            {
                try
                {
                    if (!ncs.IsConnected)
                        ncs.Connect(100);
                    ncs.ReadMode = PipeTransmissionMode.Byte;
                    Stream = ncs;
                    return;
                }
                catch (IOException)
                {
                    try { ncs.Dispose(); ncs = OpenPipe(); }
                    catch { }
                }
                catch (TimeoutException)
                {
                    StartServer();
                }
            }
            throw new InvalidProgramException(
                string.Format("failed to connect to server after {0} attempts", i));
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
                Disconnect();
            }
        }
    }
}
