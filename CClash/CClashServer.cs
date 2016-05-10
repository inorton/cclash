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

        public const int DefaultQuitAfterIdleMinutes = 90;

        public int MaxServerThreads
        {
            get
            {
                var rv = Settings.MaxServerThreads;
                if (rv == 0) rv = Environment.ProcessorCount + 1;
                return rv;
            }
        }

        public int QuitAfterIdleMinutes
        {
            get
            {
                var rv = Settings.ServerQuitAfterIdleMinutes;
                if (rv == 0) rv = DefaultQuitAfterIdleMinutes;
                return rv;
            }
        }

        List<NamedPipeServerStream> serverPipes = new List<NamedPipeServerStream>();
        List<Thread> serverThreads = new List<Thread>();

        string cdto = Path.GetPathRoot( Environment.GetFolderPath(Environment.SpecialFolder.Windows));

        public CClashServer()
        {
            Directory.SetCurrentDirectory(cdto);
        }

        Mutex serverMutex;

        public bool Preflight(string cachedir)
        {
            Logging.Emit("cclash server preflight check");
            var mtx = new Mutex(false, "cclash_serv_" + cachedir.ToLower().GetHashCode());
            serverMutex = mtx;
            try
            {
                if (!mtx.WaitOne(1000))
                {
                    quitnow = true;
                    Logging.Error("another server is already running");
                    return false; // some other process is holding it!
                }
                else
                {
                    Logging.Emit("cclash server preflight ok");
                }
            }
            catch (AbandonedMutexException)
            {
                Logging.Warning("previous instance did not exit cleanly!");
            }
            return true;
        }

        int busyThreads = 0;

        public bool FirstThreadReady { get; private set; }

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
                        FirstThreadReady = true;
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
                        }
                        
                        ThreadIsIdle();
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


            Logging.Emit("reading from client");
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
            if (Settings.PipeSecurityEveryone) {
                var npa = new PipeAccessRule("Everyone", PipeAccessRights.ReadWrite, System.Security.AccessControl.AccessControlType.Allow);
                var nps = new PipeSecurity();
                nps.AddAccessRule(npa);
                nss.SetAccessControl(nps);
            }
            t.Start(nss);
            Logging.Emit("server thread started");
        }

        public void Listen(string cachedir)
        {
            Environment.CurrentDirectory = cdto;
            Logging.Emit("creating direct cache server..");
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
                    if (busyThreads > 0)
                        Logging.Emit("{0} busy threads", busyThreads);
                    if (t.Join(1000))
                    {
                        serverThreads.Remove(t);
                        Logging.Emit("replacing thread");
                        NewServerThread(cachedir);
                    }
                }
                if (busyThreads < 1) {
                    Logging.Emit("server is idle..");
                }
                if (DateTime.Now.Subtract(lastRequest).TotalMinutes > QuitAfterIdleMinutes)
                {
                    quitnow = true;
                }
            }
            Logging.Emit("waiting for threads to finish");
            foreach (var t in serverThreads)
            {
                Logging.Emit("joining thread {0}", t.ManagedThreadId);
                if (!t.Join(2000)) {
                    Logging.Emit("thread still running..");
                }
            }

            Logging.Emit("commiting stats");
            cache.SetupStats();
            Logging.Emit("server quitting");
            serverMutex.ReleaseMutex();
        }

        public static string MakePipeName(string cachedir)
        {
            var x = cachedir.Replace('\\', ' ');
            x = x.Replace('\"', '_');
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

                case Command.DisableCache:
                    DisableCaching = true;
                    rv.supported = true;
                    break;

                case Command.ClearCache:
                    DisableCaching = true;
                    cache.SetupStats();
                    cache.Lock(CacheLockType.ReadWrite);
                    cache.OutputCache.ClearLocked();
                    cache.IncludeCache.ClearLocked();
                    cache.Unlock(CacheLockType.ReadWrite);
                    rv.supported = true;
                    break;

                case Command.EnableCache:
                    DisableCaching = false;
                    rv.supported = true;
                    break;

                case Command.Run:
                    var stdout = new StringBuilder();
                    var stderr = new StringBuilder();
                    var comp = cache.SetCompilerEx(req.pid, req.compiler, req.workdir, new Dictionary<string,string>( req.envs ));
                    cache.SetCaptureCallback(comp, (so) => { stdout.Append(so); }, (se) => { stderr.Append(se); });
                    if (DisableCaching) {
                        rv.exitcode = comp.InvokeCompiler(req.argv, null, null, false, new List<string>());
                    } else {
                        rv.exitcode = cache.CompileOrCache(comp, req.argv);
                    }
                    rv.supported = true;
                    rv.stderr = stderr.ToString();
                    rv.stdout = stdout.ToString();

                    break;

                case Command.Quit:
                    cache.SetupStats();
                    Stop();
                    break;
            }

            Logging.Emit("server resp: {0}", rv.exitcode);

            return rv;
        }

        public bool DisableCaching { get; private set; }

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
