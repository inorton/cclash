using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public sealed class Settings
    {
        public static bool DebugEnabled { get; set; }
        public static string DebugFile { get; set; }

        public static string MissLogFile { get; set; }
        public static bool MissLogEnabled { 
            get 
            {
                return !string.IsNullOrEmpty(MissLogFile); 
            }
        }

        static Settings() { }

        static bool ConditionVarsAreTrue(string prefix)
        {
            var var = Environment.GetEnvironmentVariable(prefix + "_VAR");
            if (!string.IsNullOrEmpty(var))
            {
                var values = Environment.GetEnvironmentVariable(prefix + "_VALUES");
                if (!string.IsNullOrEmpty(values))
                {
                    var check = Environment.GetEnvironmentVariable(var);
                    var vlist = values.Split(',');
                    foreach (var v in vlist)
                    {
                        if (v == check) return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Attempt to cache and restore PDB files. If the target PDB already exists then we will count 
        /// that towards the common key and cache the file. If not we mark that it doesnt and cache the file.
        /// 
        /// If on a subsequent run, the pdb already exists exactly as it was when we cached it or is missing then
        /// we allow a hit.
        /// 
        /// This basically only works for pdb builds that were sequential.
        /// </summary>
        public static bool AttemptPDBCaching
        {
            get
            {
                return Environment.GetEnvironmentVariable("CCLASH_ATTEMPT_PDB_CACHE") == "yes";
            }
        }

        /// <summary>
        /// When an object compilation with pdb generation (Zi) is requested. Instead
        /// generate embedded debug info (Z7).
        /// </summary>
        public static bool ConvertObjPdbToZ7
        {
            get
            {
                return Environment.GetEnvironmentVariable("CCLASH_Z7_OBJ") == "yes";
            }
        }

        static bool EnabledByConditions()
        {
            return ConditionVarsAreTrue("CCLASH_ENABLE_WHEN");
        }

        static bool DisabledByConditions()
        {
            return ConditionVarsAreTrue("CCLASH_DISABLE_WHEN");
        }

        public static bool Disabled
        {
            get
            {
                var dis = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CCLASH_DISABLED"));
                return (dis || DisabledByConditions()) && (!EnabledByConditions());
            }
        }

        static string cachedir = null;
        public static string CacheDirectory
        {
            get
            {
                if (cachedir == null)
                {
                    cachedir = Environment.GetEnvironmentVariable("CCLASH_DIR");
                    if (string.IsNullOrEmpty(cachedir))
                    {
                        var appdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        cachedir = System.IO.Path.Combine(appdata, "cclash");
                    }
                }
                return cachedir;
            }
            set
            {
                cachedir = value;
            }
        }

        public static bool PreprocessorMode
        {
            get
            {
                var dm = Environment.GetEnvironmentVariable("CCLASH_PPMODE");
                if (dm != null)
                {
                    return true;
                }
                return ConditionVarsAreTrue("CCLASH_PPMODE_WHEN");
            }
        }

        public static bool DirectMode
        {
            get
            {
                return !PreprocessorMode;
            }
        }

        public static bool ServiceMode
        {
            get
            {
                return Environment.GetEnvironmentVariable("CCLASH_SERVER") != null;
            }
        }

        private static int hashThreadCount;
        public static int HashThreadCount
        {
            get
            {
                if (hashThreadCount == 0) hashThreadCount = Environment.ProcessorCount;
                return hashThreadCount;
            }
            set
            {
                hashThreadCount = value;
            }
        }

        public static bool NoAutoRebuild
        {
            get
            {
                return Environment.GetEnvironmentVariable("CCLASH_AUTOREBUILD") == "no";
            }
        }
    }
}
