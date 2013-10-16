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
        /// Hash of the compiler file, cl args and source file
        /// </summary>
        public string CommonHash { get; set; }
        
        /// <summary>
        /// Hashes and names of each #included file
        /// </summary>
        public Dictionary<string, string> IncludeFiles { get; set; }

        public void ProcessIncludes( FileHasher hh, IEnumerable<string> files)
        {
            IncludeFiles = new Dictionary<string, string>();
            foreach (var f in files.Distinct())
            {
                IncludeFiles.Add(f, hh.DigestFile(f));
            }
        }

        public bool CheckIncludes(FileHasher hh)
        {
            foreach (var f in IncludeFiles.Keys)
            {
                if (!File.Exists(f))
                {
                    return false;

                }
                else
                {
                    if (hh.DigestFile(f) != IncludeFiles[f]) return false;
                }
            }
            return true;
        }
    }
}