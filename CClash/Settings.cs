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
    }
}
