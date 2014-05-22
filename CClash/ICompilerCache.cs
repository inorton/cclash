using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public interface ICompilerCache : IDisposable
    {
        ICacheInfo Stats { get; }

        bool IsSupported(ICompiler comp, IEnumerable<string> args);
        int CompileOrCache(ICompiler comp, IEnumerable<string> args, Action<string> stderr, Action<string> stdout);
        ICompiler GetCompiler(string compilerPath, string workdir, IDictionary<string, string> envs);
        DataHash DeriveHashKey(ICompiler comp, IEnumerable<string> args);
    }
}
