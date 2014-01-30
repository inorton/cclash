using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace CClash
{
    public class CClashInetServer : CClashServerBase, IDisposable
    {
        TcpListener listener;

        public CClashInetServer()
            : base()
        {
            var threadcount = Settings.HashThreadCount;
            for (int i = 0; i < threadcount; i++)
            {
                var t = new Thread(new ThreadStart(RequestThreadFunc));
                t.IsBackground = true;
                t.Start();
                threads.Add(t);
            }
        }

        public override bool Connected(Stream s)
        {
            return s.CanWrite;
        }

        public override void Stop()
        {
            quitnow = true;
            if (listener != null)
            {
                try
                {
                    listener.Stop();
                }
                catch { }
            }
        }

        public override Stream AwaitConnection(object service)
        {
            var iar = listener.BeginAcceptSocket(null, null);

            Stream rv = null;
            do
            {
                if (iar.AsyncWaitHandle.WaitOne(500))
                {
                    var sock = listener.EndAcceptSocket(iar);
                    rv = new NetworkStream(sock);
                    break;
                }
            } while (!quitnow && !iar.IsCompleted);
            return rv;
        }

        Queue<Stream> incoming = new Queue<Stream>();

        List<Thread> threads = new List<Thread>();
        AutoResetEvent enqueued = new AutoResetEvent(false);
        void RequestThreadFunc()
        {
            do
            {
                if (enqueued.WaitOne(500))
                {
                    Stream con = null;
                    lock (incoming)
                    {
                        if (incoming.Count > 0)
                        {
                            con = incoming.Dequeue();
                        }
                    }
                    if (con != null)
                    {
                        base.DoRequest(con);
                    }
                }
            } while (!quitnow);
        }

        public override void DoRequest(Stream client)
        {
            lock (incoming)
            {
                incoming.Enqueue(client);
                enqueued.Set();
            }
        }

        public override void FinishRequest(Stream clientStream)
        {
            base.FinishRequest(clientStream);
            clientStream.Close();
        }

        public override object BindStream(string cachedir)
        {
            listener = new TcpListener( IPAddress.Loopback, 0 );
            listener.Start(10);
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Settings.SetServicePort(cachedir, port);
            return listener;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
