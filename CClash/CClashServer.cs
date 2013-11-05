using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Pipes;
using System.Web.Script.Serialization;

namespace CClash
{
    public class CClashServer
    {
        bool quitnow = false;
        DirectCompilerCacheServer cache;

        int connections = 0;

        public int ExitAfterIdleSec { get; set; }

        public int MaxOperations { get; set; }

        string mydocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        public CClashServer()
        {
            ExitAfterIdleSec = 60;
            MaxOperations = 20000;
            Directory.SetCurrentDirectory(mydocs);
        }

        DateTime lastYield = DateTime.Now;
        void YieldLocks()
        {
            if (DateTime.Now.Subtract(lastYield).TotalSeconds > 5)
            {
                cache.YieldLocks();
                lastYield = DateTime.Now;
            }
        }

        public void Listen(string cachedir)
        {
            
            try
            {
                Logging.Emit("server listening..");
                using (var nss = new NamedPipeServerStream(MakePipeName(cachedir), PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.WriteThrough | PipeOptions.Asynchronous))
                {
                    cache = new DirectCompilerCacheServer(cachedir);
                    var msgbuf = new List<byte>();
                    var rxbuf = new byte[16384];
                    DateTime lastConnection = DateTime.Now;
                    
                    do
                    {
                        // don't hog folders
                        System.IO.Directory.SetCurrentDirectory(mydocs);
                        Logging.Emit("server waiting..");
                        YieldLocks();
                        try
                        {
                            connections++;
                            if (connections > MaxOperations || (DateTime.Now.Subtract(lastConnection).TotalSeconds > ExitAfterIdleSec))
                                Stop();

                            if (!nss.IsConnected)
                            {
                                var w = nss.BeginWaitForConnection(null, null);
                                while (!w.AsyncWaitHandle.WaitOne(5000))
                                {
                                    try {
                                        YieldLocks();     
                                    }
                                    catch { }
                                    if (quitnow)
                                    {
                                        return;
                                    }
                                    if (DateTime.Now.Subtract(lastConnection).TotalSeconds > 90)
                                        Stop();
                                }
                                nss.EndWaitForConnection(w);
                                lastConnection = DateTime.Now;
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
                            var req = CClashMessage.Deserialize<CClashRequest>(msgbuf.ToArray());
                            var resp = ProcessRequest(req);
                            var tx = resp.Serialize();
                            nss.Write(tx, 0, tx.Length);
                            nss.Flush();

                            // don't hog folders
                            System.IO.Directory.SetCurrentDirectory(mydocs);

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
            Logging.Emit("{0}", DateTime.Now.ToString("s"));
            Logging.Emit("server req: cmd = {0}, workdir = {1}",
                req.cmd, req.workdir);

            switch (req.cmd)
            {

                case Command.GetStats:
                    rv.exitcode = 0;
                    rv.stdout = StatOutputs.GetStatsString(req.compiler);
                    break;

                case Command.Run:
                    cache.SetCompiler(req.compiler);
                    rv.exitcode = cache.CompileOrCacheEnvs(req.workdir, req.envs, req.argv);
                    rv.supported = true;
                    rv.stderr = cache.StdErrorText.ToString();
                    rv.stdout = cache.StdOutText.ToString();
                    break;

                case Command.Quit:
                    Stop();
                    break;
            }

            Logging.Emit("server resp: {0}", rv.exitcode);

            return rv;
        }

        public void Stop()
        {
            quitnow = true;
        }
    }
}
