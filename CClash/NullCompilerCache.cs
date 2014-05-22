using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash {
    public class NullCompilerCache : CompilerCacheBase , ICompilerCache {

        public NullCompilerCache(string cachedir)
            : base(cachedir) {
        }

        public override void Setup() {
            
        }

        public override void Finished() {
            
        }

        public override bool IsSupported(ICompiler c, IEnumerable<string> args)
        {
            return false;
        }

        protected override bool CheckCache(ICompiler c, IEnumerable<string> args, DataHash commonkey, out CacheManifest manifest) {
            throw new NotImplementedException();
        }

        protected override int OnCacheMissLocked(DataHash hc, ICompiler c, IEnumerable<string> args, CacheManifest m, Action<string> stderr, Action<string> stdout)
        {
            throw new NotImplementedException();
        }
    }
}
