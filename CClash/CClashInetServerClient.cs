using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace CClash
{
    public class CClashInetServerClient : CClashServerClientBase, ICompilerCache
    {
        TcpClient client;
        string cachefolder;
        public override void Init(string cachedir)
        {
            if (cachedir == null) throw new ArgumentNullException("cachedir");
            cachefolder = cachedir;
        }

        public override void Connect()
        {
            Logging.Emit("connecting to inet server..");
            if (cachefolder == null)
            {
                throw new InvalidProgramException("Init() not called");
            }
            client = new TcpClient();
            int i;
            for (i = 0; i < 10; i++)
            {
                int port = 0;
                if (Settings.GetServicePort(cachefolder, out port))
                {
                    Logging.Emit("trying server at port {0}", port);
                    try
                    {
                        client.Connect(IPAddress.Loopback, port);
                        Stream = client.GetStream();
                        Logging.Emit("client connected..");
                        return;
                    }
                    catch (SocketException)
                    {
                        port = 0;
                        System.Threading.Thread.Sleep(500);
                        
                    }
                }
                else
                {
                    port = 0;
                }

                if (port == 0)
                {
                    if (Settings.ExitOnInetError) {
                        Console.Error.WriteLine("could not connect to service, quitting as configured");
                        System.Environment.Exit(-1);
                    }

                    Logging.Emit("could not connect, starting server");
                    StartServer();
                    System.Threading.Thread.Sleep(200);
                }
            }
            throw new InvalidProgramException(
                string.Format("failed to connect to inet server after {0} attempts", i));
        }

        public override void Disconnect()
        {
            if (client != null)
            {
                try
                {
                    client.Close();
                }
                catch { }
            }
        }

        public ICacheInfo Stats
        {
            get
            {
                return null;
            }
        }
    }
}
