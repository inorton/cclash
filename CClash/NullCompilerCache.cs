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

        public FileCacheStore OutputCache
        {
            get
            {
                return null;
            }
        }

        public override bool IsSupported(ICompiler comp, IEnumerable<string> args) {
            return false;
        }

        public override bool CheckCache(ICompiler comp, IEnumerable<string> args, DataHash commonkey, out CacheManifest manifest)
        {
            throw new NotImplementedException();
        }

        protected override int OnCacheMissLocked(ICompiler comp, DataHash hc, IEnumerable<string> args, CacheManifest m)
        {
            throw new NotImplementedException();
        }
    }
}
