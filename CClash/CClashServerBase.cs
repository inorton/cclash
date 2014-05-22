using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Pipes;
using System.Collections.Specialized;

namespace CClash
{
    public abstract class CClashServerBase
    {
        public CClashServerBase()
        {
            ExitAfterIdleSec = 60;
            MaxOperations = 20000;
            Directory.SetCurrentDirectory(mydocs);
            LastConnection = DateTime.Now;
            Settings.IsServer = true;
        }

        protected bool quitnow = false;
        protected DirectCompilerCacheServer cache;

        protected int connections = 0;

        public int ExitAfterIdleSec { get; set; }

        public int MaxOperations { get; set; }

        protected string mydocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        DateTime lastYield = DateTime.Now;

        /// <summary>
        /// Briefly unlock the cache locks to let other instances do updates.
        /// </summary>
        protected virtual void YieldLocks()
        {
            if (DateTime.Now.Subtract(lastYield).TotalSeconds > 5)
            {
                cache.YieldLocks();
                lastYield = DateTime.Now;
            }
        }

        public abstract bool Connected(Stream s);

        public virtual void DoRequest(Stream client)
        {
            if (client != null)
            {
                try
                {
                    ServeRequest(client);
                    FinishRequest(client);
                }
                catch (IOException e)
                {
                    Logging.Emit("client error {0}", e.Message);
                }
            }
        }

        public void Listen(string cachedir)
        {
            object server = null;
            try
            {
                Logging.Emit("server listening for {0}", cachedir);
                try {
                    server = BindStream(cachedir);
                } catch (CClashServerStartedException) {
                    Logging.Error("server already running for {0}", cachedir);
                    return;
                }

                cache = new DirectCompilerCacheServer(cachedir);
                do
                {
                    Logging.Emit("server waiting..");
                    YieldLocks();
                    try
                    {
                        var con = AwaitConnection(server);
                        this.DoRequest(con);
                    }
                    catch (Exception e)
                    {
                        Logging.Error("server exception {0}", e);
                        Stop();
                    }
                } while (!quitnow);
                Logging.Emit("server quitting");

            }
            catch (IOException ex)
            {
                Logging.Emit("{0}", ex);
                return;
            }
            finally
            {
                if (server != null)
                {
                    if (server is IDisposable)
                    {
                        ((IDisposable)server).Dispose();
                    }
                }
            }
        }
        public abstract void Stop();

        public DateTime LastConnection { get; protected set; }

        public int ConnectionCount { get; protected set; }

        public abstract Stream AwaitConnection(object service);

        public abstract object BindStream(string cachedir);

        public void ServeRequest(Stream clientStream)
        {
            var rxbuf = new StringBuilder();
            Logging.Emit("server connected..");
            var reader = new StreamReader(clientStream);

            string line;
            do
            {
                line = reader.ReadLine();
                rxbuf.AppendLine(line);
                if (line == ":end:") break;
                if (line == null) break;
            } while (true);

            // deserialize message from msgbuf
            var req = new CClashRequest();
            req.Deserialize(rxbuf.ToString());
            cache.Setup();
            var resp = ProcessRequest(req);
            var tx = resp.ToBytes();
            clientStream.Write(tx, 0, tx.Length);
        }

        public CClashResponse ProcessRequest(CClashRequest req)
        {
            var rv = new CClashResponse() { supported = false };
            Logging.Emit("{0}", DateTime.Now.ToString("s"));
            Logging.Emit("server req: cmd = {0}, workdir = {1}",
                req.cmd, req.workdir);

            switch (req.cmd)
            {
                case Command.NoOp:
                    rv.supported = true;
                    rv.stderr = "no op";
                    rv.stdout = req.argv.FirstOrDefault();
                    break;

                case Command.GetStats:
                    rv.exitcode = 0;
                    cache.SetupStats(); // commits stats to disk

                    rv.stdout = StatOutputs.GetStatsString(req.compiler);
                    break;

                case Command.Run:
                    var comp = cache.GetCompiler(req.compiler, req.workdir, req.envs);
                    var tmperr = new StringBuilder();
                    var tmpout = new StringBuilder();
                    rv.exitcode = cache.CompileOrCache(comp as Compiler, req.argv, (er) => tmperr.Append(er), (ou) => tmpout.Append(ou));
                    rv.supported = true;
                    rv.stderr = tmperr.ToString();
                    rv.stdout = tmpout.ToString();
                    break;

                case Command.Quit:
                    Stop();
                    break;
            }

            Logging.Emit("server resp: {0}", rv.exitcode);

            return rv;
        }

        public virtual void FinishRequest(Stream clientStream)
        {

            // don't hog folders
            cache.Finished();
        }
    }
}
