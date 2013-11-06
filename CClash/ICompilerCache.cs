using System;
using System.Collections.Generic;
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
        void SetCompiler(string compiler);
        DataHash DeriveHashKey(IEnumerable<string> args);
    }
}
