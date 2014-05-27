using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Pipes;
using System.Web.Script.Serialization;
using System.Threading;

namespace CClash
{
    public sealed class CClashServer : IDisposable
    {
        bool quitnow = false;
        DirectCompilerCacheServer cache;

        /// <summary>
        /// The maximum number of pending requests.
        /// </summary>
        public const int MaxServerThreads = 20;

        public const int QuitAfterIdleMinutes = 10;

        List<NamedPipeServerStream> serverPipes = new List<NamedPipeServerStream>();
        List<Thread> serverThreads = new List<Thread>();

        string mydocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        public CClashServer()
        {
            Directory.SetCurrentDirectory(mydocs);
        }

        int busyThreads = 0;

        public int BusyThreadCount
        {
            get
            {
                return busyThreads;
            }
        }

        void ThreadIsBusy()
        {
            lock (serverThreads)
            {
                busyThreads++;
            }
        }

        void ThreadIsIdle()
        {
            lock (serverThreads)
            {
                busyThreads--;
            }
        }

        DateTime lastRequest = DateTime.Now;

        void ThreadBeforeProcessRequest()
        {
            lastRequest = DateTime.Now;
            if (BusyThreadCount > Environment.ProcessorCount)
            {
                System.Threading.Thread.Sleep(60/Environment.ProcessorCount);
            }
        }

        public void ConnectionThreadFn(object con)
        {
            using (var nss = con as NamedPipeServerStream)
            {
                try
                {
                    
                    while (!quitnow)
                    {
                        var w = nss.BeginWaitForConnection(null, null);
                        Logging.Emit("waiting for client..");
                        while (!w.AsyncWaitHandle.WaitOne(1000))
                        {
                            if (quitnow)
                            {
                                return;
                            }
                        }
                        nss.EndWaitForConnection(w);
                        Logging.Emit("got client");
                        if (nss.IsConnected)
                        {
                            Logging.Emit("server connected");
                            ThreadBeforeProcessRequest();
                            ThreadIsBusy();
                            ServiceRequest(nss);
                            ThreadIsIdle();
                        }
                    }
                }
                catch (IOException ex)
                {
                    Logging.Error("server thread got {0}, {1}", ex.GetType().Name, ex.Message);
                    Logging.Error(":{0}", ex.ToString());
                }
            }
        }

        public void ServiceRequest(NamedPipeServerStream nss)
        {
            var msgbuf = new List<byte>(8192);
            var rxbuf = new byte[256 * 1024];
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
            cache.Setup(); // needed?
            Logging.Emit("processing request");
            var resp = ProcessRequest(req);
            Logging.Emit("request complete: supported={0}, exitcode={1}", resp.supported, resp.exitcode);
            var tx = resp.Serialize();
            nss.Write(tx, 0, tx.Length);
            nss.Flush();
            Logging.Emit("server written {0} bytes", tx.Length);

            nss.WaitForPipeDrain();
            nss.Disconnect();
            Logging.Emit("request done");
        }

        void NewServerThread(string cachedir)
        {
            var t = new Thread(new ParameterizedThreadStart(ConnectionThreadFn));
            t.IsBackground = true;
            serverThreads.Add(t);
            var nss = new NamedPipeServerStream(MakePipeName(cachedir), PipeDirection.InOut, MaxServerThreads, PipeTransmissionMode.Message, PipeOptions.WriteThrough | PipeOptions.Asynchronous);
            t.Start(nss);
            Logging.Emit("server thread started");
        }

        public void Listen(string cachedir)
        {
            Environment.CurrentDirectory = mydocs;
            var mtx = new Mutex(false, "cclash_serv_" + cachedir.ToLower().GetHashCode());
            try
            {

                if (!mtx.WaitOne(1000))
                {
                    quitnow = true;
                    Logging.Error("another server is already running");
                    return; // some other process is holding it!
                }
            }
            catch (AbandonedMutexException)
            {
                Logging.Warning("previous instance did not exit cleanly!");
            }
            cache = new DirectCompilerCacheServer(cachedir);
            Logging.Emit("starting server threads..");

            while (serverThreads.Count < MaxServerThreads)
            {
                NewServerThread(cachedir);
            }

            // maintain the threadpool
            while (!quitnow)
            {
                foreach (var t in serverThreads.ToArray())
                {
                    if (t.Join(1000))
                    {
                        serverThreads.Remove(t);
                        NewServerThread(cachedir);
                    }
                    if (DateTime.Now.Subtract(lastRequest).TotalMinutes > QuitAfterIdleMinutes)
                    {
                        quitnow = true;
                    }
                }
            }
            foreach (var t in serverThreads)
            {
                t.Join(2000);
            }


            Logging.Emit("server quitting");
            mtx.ReleaseMutex();
            
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
                    
                    rv.stdout = StatOutputs.GetStatsString(req.compiler, cache);
                    break;

                case Command.Run:
                    cache.SetCompilerEx(req.pid, req.compiler, req.workdir, new Dictionary<string,string>( req.envs ));
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
