using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public class DirectCompilerCacheServer : DirectCompilerCache
    {

        public DirectCompilerCacheServer(string cachedir)
            : base(cachedir)
        {
            StdErrorText = new StringBuilder();
            StdOutText = new StringBuilder();
        }

        public StringBuilder StdErrorText { get; private set; }
        public StringBuilder StdOutText { get; private set; }

        public override void OutputWriteLine(string str)
        {
            StdOutText.AppendLine(str);
        }

        public override void ErrorWriteLine(string str)
        {
            StdErrorText.AppendLine(str);
        }

        public int CompileOrCacheEnvs( IDictionary<string,string> envs, IEnumerable<string> args)
        {
            foreach (var e in envs)
            {
                Environment.SetEnvironmentVariable(e.Key, e.Value);
            }
            return base.CompileOrCache(args);
        }
    }
}
