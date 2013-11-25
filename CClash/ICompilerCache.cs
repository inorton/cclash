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

        bool IsSupported(IEnumerable<string> args);
        int CompileOrCache(IEnumerable<string> args);
        void SetCompiler(string compilerPath, string workdir, IDictionary<string,string> envs);
        DataHash DeriveHashKey(IEnumerable<string> args);
    }
}
