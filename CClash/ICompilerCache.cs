using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public interface ICompilerCache : IDisposable
    {
        CacheStats Stats { get; }

        bool IsSupported(IEnumerable<string> args);
        int CompileOrCache(IEnumerable<string> args);
        DataHash DeriveHashKey(IEnumerable<string> args);
    }
}
