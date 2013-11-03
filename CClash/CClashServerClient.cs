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
    public class CClashServerClient : ICompilerCache
    {
        NamedPipeClientStream ncs;
        JavaScriptSerializer jss;
        string pipename = null;
        public CClashServerClient(string cachedir)
        {
            pipename = CClashServer.MakePipeName(cachedir);
            
            jss = new JavaScriptSerializer();
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

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    if (!ncs.IsConnected)
                        ncs.Connect(100);
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
                    var p = new Process();
                    var psi = new ProcessStartInfo(exe);
                    psi.CreateNoWindow = true;
                    psi.Arguments = "--cclash-server";
                    psi.ErrorDialog = false;
                    psi.WorkingDirectory = Environment.CurrentDirectory;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                    p.StartInfo = psi;
                    p.Start();
                }
            }
            throw new InvalidProgramException("server failed to start");
        }

        public CacheStats Stats
        {
            get { throw new NotImplementedException(); }
        }

        public bool IsSupported(IEnumerable<string> args)
        {
            return true;
        }

        string compilerPath;

        public void SetCompiler(string compiler)
        {
            if (string.IsNullOrEmpty(compiler)) throw new ArgumentNullException("compiler");
            
            compilerPath = System.IO.Path.GetFullPath(compiler);
        }

        public int CompileOrCache(IEnumerable<string> args)
        {
            var envs = new Dictionary<string,string>();
            envs["INCLUDE"] = Environment.GetEnvironmentVariable("INCLUDE");
            envs["LIB"] = Environment.GetEnvironmentVariable("LIB");
            envs["PATH"] = Environment.GetEnvironmentVariable("PATH");

            var req = new CClashRequest()
            {
                cmd = Command.Run,
                compiler = compilerPath,
                envs = envs,
                workdir = Environment.CurrentDirectory,
                argv = new List<string> ( args ),
            };
            var resp = Transact(req);
            if (resp != null)
            {

                Console.Error.Write(resp.stderr);
                Console.Out.Write(resp.stdout);

                return resp.exitcode;
            }

            return -1;
            
        }

        public CClashResponse Transact(CClashRequest req)
        {
            Connect();
            CClashResponse resp = null;
            var js = jss.Serialize(req);
            var buf = UnicodeEncoding.Unicode.GetBytes(js);

            ncs.Write(buf, 0, buf.Length);
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
                resp = jss.Deserialize<CClashResponse>(UnicodeEncoding.Unicode.GetString(rx.ToArray()));

                ncs.Close();
            }
            return resp;
        }

        public DataHash DeriveHashKey(IEnumerable<string> args)
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
            if ( ncs != null ) ncs.Dispose();
        }
    }
}
