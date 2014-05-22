using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash {
    public class CacheSessionContext 
    {
        public ICompilerCache Cache { get; set; }
        public Compiler Compiler { get; set; }
        public StringBuilder StdOutput { get; set; }
        public StringBuilder StdError { get; set; }
        DateTime OperationStart { get; set; }

        public virtual bool IsSupported(IEnumerable<string> args) {
            OperationStart = DateTime.Now;
            if (FileUtils.Exists(Compiler.CompilerExe))
            {
                var rv = Compiler.ProcessArguments(args.ToArray());
                if (!rv) {
                    Logging.Emit("args not supported {0}", Cache.GetType().Name);
                }
                return rv;
            }
            throw new FileNotFoundException(Compiler.CompilerExe);
        }


        public void CopyOutputFiles(DataHash hc) 
        {
            try {
                Cache.CopyFile(Cache.OutputCache.MakePath(hc.Hash, CompilerCacheBase.F_Object), Compiler.ObjectTarget);
                if (Compiler.GeneratePdb)
                    Cache.CopyFile(Cache.OutputCache.MakePath(hc.Hash, CompilerCacheBase.F_Pdb), Compiler.PdbFile);
            } catch (Exception e) {
                Logging.Error("{0}", e);
                throw;
            }
        }

        public void CopyStdio(DataHash hc) 
        {
            var stderrfile = Cache.OutputCache.MakePath(hc.Hash, CompilerCacheBase.F_Stderr);
            var stdoutfile = Cache.OutputCache.MakePath(hc.Hash, CompilerCacheBase.F_Stdout);

            StdOutput.Clear();
            StdError.Clear();
            StdOutput.Append(File.ReadAllText(stdoutfile));
            StdError.Append(File.ReadAllText(stderrfile));
        }
    }
}
