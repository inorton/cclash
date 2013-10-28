using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public class CacheManifest
    {
        /// <summary>
        /// When this manifest was created.
        /// </summary>
        public string TimeStamp { get; set; }

        /// <summary>
        /// How long the original build took.
        /// </summary>
        public int Duration { get; set; }

        /// <summary>
        /// Hash of the compiler file, cl args and source file
        /// </summary>
        public string CommonHash { get; set; }
        
        /// <summary>
        /// Hashes and names of each #included file
        /// </summary>
        public Dictionary<string, string> IncludeFiles { get; set; }

        /// <summary>
        /// A list of files that did not exist but will require a rebuild if they are added.
        /// </summary>
        public List<string> PotentialNewIncludes { get; set; }

        public void ProcessIncludes( HashUtil hh, IEnumerable<string> files)
        {
            IncludeFiles = new Dictionary<string, string>();
            foreach (var f in files.Distinct())
            {
                var dg = hh.DigestSourceFile(f);
                if (dg.Result == DataHashResult.Ok)
                {
                    IncludeFiles.Add(f, dg.Hash);
                }
            }
        }

        public bool CheckIncludes(HashUtil hh)
        {
            foreach (var f in IncludeFiles.Keys)
            {
                if (!FileUtils.Exists(f))
                {
                    return false;

                }
                else
                {
                    var dg = hh.DigestSourceFile(f);
                    
                    if (dg.Result != DataHashResult.Ok || (dg.Hash != IncludeFiles[f])) return false;
                }
            }
            return true;
        }
    }
}