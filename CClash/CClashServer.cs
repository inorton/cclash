using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Pipes;
using System.Web.Script.Serialization;

namespace CClash
{
    public class CClashServer
    {
        bool quitnow = false;
        DirectCompilerCacheServer cache;

        DateTime started = DateTime.Now;
        int connections = 0;

        public int ExitAfterSec { get; set; }

        public int MaxOperations { get; set; }

        public CClashServer()
        {
            ExitAfterSec = 300;
            MaxOperations = 20000;
        }

        public void Listen(string cachedir)
        {
            try
            {
                Logging.Emit("server listening..");
                using (var nss = new NamedPipeServerStream(MakePipeName(cachedir), PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.WriteThrough | PipeOptions.Asynchronous))
                {
                    cache = new DirectCompilerCacheServer(cachedir);
                    var jss = new JavaScriptSerializer();
                    var msgbuf = new List<byte>();
                    var rxbuf = new byte[16384];

                    do
                    {
                        Logging.Emit("server waiting..");

                        try
                        {
                            connections++;
                            if (connections > MaxOperations || (DateTime.Now.Subtract(started).TotalSeconds > ExitAfterSec))
                                Stop();

                            if (!nss.IsConnected)
                            {
                                var w = nss.BeginWaitForConnection(null, null);
                                while (!w.AsyncWaitHandle.WaitOne(2000))
                                {
                                    if (quitnow)
                                    {
                                        return;
                                    }
                                }
                                nss.EndWaitForConnection(w);
                            }

                            Logging.Emit("server connected..");

                            msgbuf.Clear();
                            int count = 0;
                            do
                            {
                                count = nss.Read(rxbuf, msgbuf.Count, rxbuf.Length);
                                if (count > 0)
                                {
                                    msgbuf.AddRange(rxbuf.Take(count));
                                }

                            } while (!nss.IsMessageComplete);

                            Logging.Emit("server read  {0} bytes", msgbuf.Count);

                            // deserialize message from msgbuf
                            var req = jss.Deserialize<CClashRequest>(UnicodeEncoding.Unicode.GetString(msgbuf.ToArray()));
                            var resp = ProcessRequest(req);
                            var jresp = jss.Serialize(resp);
                            var tx = UnicodeEncoding.Unicode.GetBytes(jresp);
                            nss.Write(tx, 0, tx.Length);
                            nss.Flush();
                            nss.WaitForPipeDrain();
                            nss.Disconnect();
                        }
                        catch (Exception e)
                        {
                            Logging.Emit("server exception {0}", e);
                        }
                    } while (!quitnow);
                    Logging.Emit("server quitting");
                }
            }
            catch (IOException ex)
            {
                Logging.Emit("{0}", ex);
                return;
            }
        }

        public static string MakePipeName(string cachedir)
        {
            var x = cachedir.Replace('\\', ' ');
            return x.Replace(':', '=') + ".pipe";
        }

        public CClashResponse ProcessRequest(CClashRequest req)
        {
            var rv = new CClashResponse() { supported = false };

            switch (req.cmd)
            {
                // nothing actually sends this yet
                case Command.GetStats:
                    rv.exitcode = 0;
                    rv.stdout = StatOutputs.GetStatsString(req.compiler);
                    break;

                case Command.Run:
                    cache.SetCompiler(req.compiler);
                    rv.exitcode = cache.CompileOrCacheEnvs(req.envs, req.argv);
                    rv.supported = true;
                    rv.stderr = cache.StdErrorText.ToString();
                    rv.stdout = cache.StdOutText.ToString();
                    break;

                case Command.Quit:
                    Stop();
                    break;
            }

            return rv;
        }

        public void Stop()
        {
            quitnow = true;
        }
    }
}
