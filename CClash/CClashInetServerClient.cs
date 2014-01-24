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
            cachefolder = cachedir;
        }

        public override void Connect()
        {
            client = new TcpClient();
            int i;
            for (i = 0; i < 10; i++)
            {
                int port = 0;
                if (Settings.GetServicePort(cachefolder, out port))
                {
                    try
                    {
                        client.Connect(IPAddress.Loopback, port);
                        Stream = client.GetStream();
                        return;
                    }
                    catch (SocketException)
                    {
                        port = 0;
                    }
                }
                else
                {
                    port = 0;
                }

                if (port == 0)
                {
                    StartServer();
                    System.Threading.Thread.Sleep(50);
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
