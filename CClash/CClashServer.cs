using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Pipes;
using System.Web.Script.Serialization;

namespace CClash
{
    public sealed class CClashServer : IDisposable
    {
        bool quitnow = false;
        DirectCompilerCacheServer cache;

        int connections = 0;

        string mydocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        public CClashServer()
        {
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
                    var rxbuf = new byte[256*1024];
                    DateTime lastConnection = DateTime.Now;
                    
                    do
                    {
                        // don't hog folders
                        System.IO.Directory.SetCurrentDirectory(mydocs);
                        Logging.Emit("server waiting..");
                        YieldLocks();
                        try {
                            connections++;

                            if (!nss.IsConnected) {
                                var w = nss.BeginWaitForConnection(null, null);
                                while (!w.AsyncWaitHandle.WaitOne(5000)) {
                                    try {
                                        YieldLocks();
                                    } catch { }
                                    if (quitnow) {
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
                            do {
                                count = nss.Read(rxbuf, msgbuf.Count, rxbuf.Length);
                                if (count > 0) {
                                    msgbuf.AddRange(rxbuf.Take(count));
                                }

                            } while (!nss.IsMessageComplete);

                            Logging.Emit("server read  {0} bytes", msgbuf.Count);

                            // deserialize message from msgbuf
                            var req = CClashMessage.Deserialize<CClashRequest>(msgbuf.ToArray());
                            cache.Setup();
                            var resp = ProcessRequest(req);
                            var tx = resp.Serialize();
                            nss.Write(tx, 0, tx.Length);
                            nss.Flush();

                            // don't hog folders
                            cache.Finished();


                            nss.WaitForPipeDrain();
                            nss.Disconnect();
                            Logging.Emit("server disconnected..");
                        } catch (IOException) {
                            Logging.Warning("error on client pipe");
                            nss.Disconnect();
                            
                        } catch (Exception e) {
                            Logging.Error("server exception {0}", e);
                            Stop();
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
                    cache.SetupStats(); // commits stats to disk
                    
                    rv.stdout = StatOutputs.GetStatsString(req.compiler);
                    break;

                case Command.Run:
                    cache.SetCompiler(req.compiler, req.workdir, new Dictionary<string,string>( req.envs ));
                    rv.exitcode = cache.CompileOrCache(req.argv);
                    System.IO.Directory.SetCurrentDirectory(mydocs);
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
            Dispose(true);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if ( cache != null ) cache.Dispose();
            }
        }
    }
}
