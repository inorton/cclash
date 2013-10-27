using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public class Settings
    {
        public static bool DebugEnabled { get; set; }
        public static string DebugFile { get; set; }

        static bool DisabledByConditions()
        {
            var var = Environment.GetEnvironmentVariable("CCLASH_DISABLE_WHEN_VAR");
            if (!string.IsNullOrEmpty(var))
            {
                var values = Environment.GetEnvironmentVariable("CCLASH_DISABLE_WHEN_VALUES");
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

        public static bool Disabled
        {
            get
            {
                var dis = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CCLASH_DISABLED"));
                return (dis || DisabledByConditions());
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
                        cachedir = System.IO.Path.Combine(appdata, "clcache-data");
                    }
                }
                return cachedir;
            }
            set
            {
                cachedir = value;
            }
        }
    }
}
