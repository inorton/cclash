using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public enum Command
    {
        Run = 0,
        GetStats = 1,
        Quit = 2,
    }

    public class CClashRequest
    {
        public Command cmd;
        public IDictionary<string, string> envs;
        public IList<string> argv;
        public string compiler;
    }

    public class CClashResponse
    {
        public bool supported;
        public int exitcode;
        public string stderr;
        public string stdout;
    }
}
