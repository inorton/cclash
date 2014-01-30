using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public sealed class CClashPipeServer : CClashServerBase, IDisposable
    {
        public CClashPipeServer()
            : base()
        {
        }

        public override object BindStream(string cachedir)
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

        public override Stream AwaitConnection(object server)
        {
            var nss = (NamedPipeServerStream)server;
            ConnectionCount++;
            if (ConnectionCount > MaxOperations || (DateTime.Now.Subtract(LastConnection).TotalSeconds > ExitAfterIdleSec))
            {
                Stop();
                return null;
            }

            if (!Connected(nss))
            {
                var w = nss.BeginWaitForConnection(null, null);
                while (!w.AsyncWaitHandle.WaitOne(5000))
                {
                    try
                    {
                        YieldLocks();
                    }
                    catch { }
                    if (quitnow)
                    {
                        return null;
                    }
                    if (DateTime.Now.Subtract(LastConnection).TotalSeconds > 90)
                        Stop();
                }
                nss.EndWaitForConnection(w);
                LastConnection = DateTime.Now;
            }
            return nss;
        }

        public override void FinishRequest(Stream clientStream)
        {
            clientStream.Flush();
            base.FinishRequest(clientStream);

            var nss = (NamedPipeServerStream)clientStream;

            nss.WaitForPipeDrain();
            nss.Disconnect();
        }

        public static string MakePipeName(string cachedir)
        {
            var x = cachedir.Replace('\\', ' ');
            return x.Replace(':', '=') + ".pipe";
        }

        public override void Stop()
        {
            quitnow = true;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
