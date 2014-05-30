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
        FileCacheStore OutputCache { get; }
        bool CheckCache(ICompiler comp, IEnumerable<string> args, DataHash commonkey, out CacheManifest manifest);
        ICompiler SetCompiler(string compiler, string workdir, Dictionary<string, string> envs);
        bool IsSupported( ICompiler comp, IEnumerable<string> args);
        int CompileOrCache( ICompiler comp, IEnumerable<string> args);
        void SetCaptureCallback(ICompiler comp, Action<string> onOutput, Action<string> onError);
        DataHash DeriveHashKey(ICompiler comp, IEnumerable<string> args);
    }
}
