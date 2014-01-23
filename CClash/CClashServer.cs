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
        }

        protected bool quitnow = false;
        protected DirectCompilerCacheServer cache;

        protected int connections = 0;

        public int ExitAfterIdleSec { get; set; }

        public int MaxOperations { get; set; }

        protected string mydocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        DateTime lastYield = DateTime.Now;
        protected void YieldLocks()
        {
            if (DateTime.Now.Subtract(lastYield).TotalSeconds > 5)
            {
                cache.YieldLocks();
                lastYield = DateTime.Now;
            }
        }

        public abstract bool Connected(Stream s);

        public abstract void Listen(string cachedir);

        public abstract void Stop();

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
    }

    public sealed class CClashPipeServer :  CClashServerBase, IDisposable
    {

        public CClashPipeServer() : base()
        {
        }


        NamedPipeServerStream BindStream(string cachedir)
        {
            return new NamedPipeServerStream(MakePipeName(cachedir), PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.WriteThrough | PipeOptions.Asynchronous);
        }

        public override bool Connected(Stream s)
        {
            if (s is NamedPipeServerStream)
            {
                return ((NamedPipeServerStream)s).IsConnected;
            }

            return false;
        }


        public override void Listen(string cachedir)
        {
            
            try
            {
                Logging.Emit("server listening..");
                using (var nss = BindStream(cachedir) )
                {
                    cache = new DirectCompilerCacheServer(cachedir);

                    var rxbuf = new StringBuilder();
                    DateTime lastConnection = DateTime.Now;
                 
                    do
                    {
                        Logging.Emit("server waiting..");
                        YieldLocks();
                        try
                        {
                            connections++;
                            if (connections > MaxOperations || (DateTime.Now.Subtract(lastConnection).TotalSeconds > ExitAfterIdleSec))
                            {
                                Stop();
                                break;
                            }

                            if (!Connected(nss))
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
                            var reader = new StreamReader(nss);

                            string line;
                            rxbuf.Clear();
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
                            nss.Write(tx, 0, tx.Length);
                            nss.Flush();

                            // don't hog folders
                            cache.Finished();
                            
                            nss.WaitForPipeDrain();
                            nss.Disconnect();
                        }
                        catch (Exception e)
                        {
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

        public override void Stop()
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
