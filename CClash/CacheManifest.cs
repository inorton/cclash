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
        /// Next time this job appears, just run the compiler.
        /// </summary>
        public bool Disable { get; set; }

        /// <summary>
        /// When this manifest was created.
        /// </summary>
        public string TimeStamp { get; set; }

        /// <summary>
        /// How long the original build took (msec).
        /// </summary>
        public int Duration { get; set; }

        /// <summary>
        /// Hash of the compiler file, cl args and source file
        /// </summary>
        public string CommonHash { get; set; }

        /// <summary>
        /// non-null if this entry was made by preprocessing the source
        /// </summary>
        public string PreprocessedSourceHash { get; set; }

        /// <summary>
        /// Hashes and names of each #included file
        /// </summary>
        public Dictionary<string, string> IncludeFiles { get; set; }

        /// <summary>
        /// A list of files that did not exist but will require a rebuild if they are added.
        /// </summary>
        public List<string> PotentialNewIncludes { get; set; }

        public int ExitCode { get; set; }
    }
}