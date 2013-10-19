using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Web.Script.Serialization;

namespace CClash
{
    public class CompilerCache
    {
        JavaScriptSerializer jss = new JavaScriptSerializer();
        FileCacheStore cache;
        String compilerPath;
        HashUtil hasher = new HashUtil();
        Compiler comp;

        const string F_Manifest = "manifest.json";
        const string F_Object = "target.object";
        const string F_Pdb = "target.pdb";
        const string F_Stdout = "compiler.stdout";
        const string F_Stderr = "compiler.stderr";

        public CompilerCache(string cacheFolder, string compiler)
        {
            cache = FileCacheStore.Load(cacheFolder);
            compilerPath = System.IO.Path.GetFullPath(compiler);
            comp = new Compiler() {
                CompilerExe = compilerPath 
            };
        }

        public bool IsSupported(IEnumerable<string> args)
        {
            if (File.Exists(compilerPath))
            {
                return comp.ProcessArguments(args.ToArray());
            }
            throw new FileNotFoundException(compilerPath);
        }

        public DataHash DeriveHashKey( IEnumerable<string> args )
        {
            var comphash = hasher.DigestFile(compilerPath);
            if (comphash.Result == DataHashResult.Ok)
            {
                var buf = new StringBuilder();
                var incs = Environment.GetEnvironmentVariable("INCLUDE");
                if (incs != null)
                    buf.AppendLine(incs);
                foreach (var a in args)
                    buf.AppendLine(a);
                buf.AppendLine(comphash.Hash);
                comphash = hasher.DigestString(buf.ToString());
            }
            return comphash;
        }

        public bool CheckCache(DataHash commonkey)
        {
            if (cache.ContainsEntry(commonkey.Hash, F_Manifest))
            {
                var mn = cache.MakePath(commonkey.Hash, F_Manifest);
                var m = jss.Deserialize<CacheManifest>(File.ReadAllText(mn));
                foreach ( var f in m.PotentialNewIncludes ) {
                    if (File.Exists(f)) return false;
                }
                foreach (var ent in m.IncludeFiles)
                {
                    var h = hasher.DigestFile(ent.Key);
                    if (h.Result == DataHashResult.Ok)
                    {
                        if (h.Hash != ent.Value) return false;
                    } else
                    {
                        return false;
                    }
                }
                return true; // cache hit, all includes match and no new files added
            }
            return false;
        }
    }
}
