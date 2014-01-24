using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public sealed class CClashPipeServerClient : CClashServerClientBase, ICompilerCache
    {
        string pipename = null;
        
        public override void Init(string cachedir)
        {
            pipename = CClashPipeServer.MakePipeName(cachedir);
        }

        NamedPipeClientStream OpenPipe()
        {
            return new NamedPipeClientStream(".", pipename, PipeDirection.InOut);
        }

        public override void Disconnect()
        {
            if (Stream != null)
            {
                Stream.Close();
            }
            Stream = null;
        }

        public override void Connect()
        {
            var ncs = OpenPipe();
            int i;
            for (i = 0; i < 10; i++)
            {
                try
                {
                    if (!ncs.IsConnected)
                        ncs.Connect(100);
                    ncs.ReadMode = PipeTransmissionMode.Byte;
                    Stream = ncs;
                    return;
                }
                catch (IOException)
                {
                    try { ncs.Dispose(); ncs = OpenPipe(); }
                    catch { }
                }
                catch (TimeoutException)
                {
                    StartServer();
                }
            }
            throw new InvalidProgramException(
                string.Format("failed to connect to pipe server after {0} attempts", i));
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
