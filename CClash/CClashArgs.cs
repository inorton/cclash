using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Options;

namespace CClash {
    public enum ServerMode {
        None,
        Pipes,
        Inet,
    }

    public class CClashArgs {

        const ServerMode DefaultMode = ServerMode.Pipes;

        public void Parse(string[] argv) 
        {
            if (argv.Length > 0 && argv[0] == "--cclash")
            {
                Matched = true;
                var opts = new OptionSet();
                opts.Add("server", "start the cclash server", (x) => {
                    if (ServerMode == ServerMode.None) {
                        ServerMode = DefaultMode;
                    }
                });

                opts.Add("stop", "stop the cclash server", (x) => {
                    StopServer = true;
                });

                opts.Add("inet", "use TCP/IP sockets", (x) => {
                    ServerMode = ServerMode.Inet;
                });

                opts.Add("pipes", "use named pipes (default)", (x) => {
                    ServerMode = ServerMode.Pipes;
                });

                opts.Add("debug=", "log to a file", (string x) => { 
                    Settings.DebugFile = x; 
                    Settings.DebugEnabled = true;
                });

                opts.Parse(argv);

                WantStats = (!StopServer && ServerMode == ServerMode.None);
                if (ServerMode != CClash.ServerMode.None)
                    Settings.IsServer = true;
            }
        }

        public bool Matched { get; private set; }
        public ServerMode ServerMode { get; private set; }
        public bool StopServer { get; private set; }
        public bool WantStats { get; private set; }
    }
}
