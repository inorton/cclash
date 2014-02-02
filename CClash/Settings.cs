using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Win32;

namespace CClash
{
    public sealed class Settings
    {
        public const ushort DefaultInetPort = 33333;

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

        static bool ConditionVarsAreTrue(string prefix, Dictionary<string, string> envs )
        {
            string var = null;
            string varvalue = null;
            if (envs.TryGetValue(prefix + "_VAR", out var) && envs.TryGetValue( var, out varvalue ) )
            {
                string values = null;
                if (envs.TryGetValue(prefix + "_VALUES", out values))
                {
                    var vlist = values.Split(',');
                    foreach (var v in vlist)
                    {
                        if (v == varvalue) return true;
                    }
                }
            }
            return false;
        }

        static bool EnabledByConditions(Dictionary<string, string> envs)
        {
            return ConditionVarsAreTrue("CCLASH_ENABLE_WHEN", envs);
        }

        static bool DisabledByConditions(Dictionary<string, string> envs)
        {
            return ConditionVarsAreTrue("CCLASH_DISABLE_WHEN", envs);
        }

        static Dictionary<string, string> getEdict()
        {
            var envs = Environment.GetEnvironmentVariables();
            var edict = new Dictionary<string, string>(envs.Count);
            foreach (var n in envs.Keys)
            {
                edict.Add((string)n, (string)envs[n]);
            }
            return edict;
        }

        public static bool Disabled
        {
            get
            {
                var edict = getEdict();
                return edict.ContainsKey("CCLASH_DISABLED") || DisabledByConditions(edict) && (!EnabledByConditions(edict));
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
                return false;
            }
        }

        public static bool DirectMode
        {
            get
            {
                return !PreprocessorMode;
            }
        }

        public static bool ExcludeOutputObjectPathFromCommonHash
        {
            get
            {
                return Environment.GetEnvironmentVariable("CCLASH_HASH_NO_OBJECTARG") != null;
            }
        }

        public static bool ServiceMode
        {
            get
            {
                return Environment.GetEnvironmentVariable("CCLASH_SERVER") != null;
            }
        }

        public static bool InetServiceMode
        {
            get
            {
                var sm = Environment.GetEnvironmentVariable("CCLASH_SERVER");
                if (sm == null) return false;
                //Console.Error.WriteLine("=== CCLASH_SERVER={0};", sm);
                var rv = sm.Equals("inet",StringComparison.InvariantCultureIgnoreCase);
                //Console.Error.WriteLine("=== CCLASH_SERVER={0} {1};", sm, rv);
                return rv;
            }
        }

        public static bool ExitOnInetError {
            get {
                return Environment.GetEnvironmentVariable("CCLASH_EXIT_ON_INETERROR") != null;
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

        public static bool TryHardLinks
        {
            get
            {
                return Environment.GetEnvironmentVariable("CCLASH_TRY_HARDLINKS") != null;
            }
        }

        public static bool BypassPotentialIncludeChecks
        {
            get
            {
                return Environment.GetEnvironmentVariable("CCLASH_LAZY_NEW_INCLUDES") != null;

            }
        }

        public static bool GetServicePortEnv( out int portnumber )
        {
            portnumber = -1;
            var ev = Environment.GetEnvironmentVariable("CCLASH_SERVER_PORT");
            if (ev != null) {
                return Int32.TryParse(ev, out portnumber);
            }
            return false; 
        }

        public static bool GetServicePort(string cachedir, out int portnumber)
        {
            if (!GetServicePortEnv(out portnumber)) {
                portnumber = DefaultInetPort;
            }
            return true;
        }

        public static void SetServicePort(string cachedir, int portnumber)
        {
            if (portnumber < 1024 || portnumber > ushort.MaxValue) {
                throw new ArgumentOutOfRangeException("portnumber", "port must be between 1024 and " + ushort.MaxValue);
            }
            Environment.SetEnvironmentVariable("CCLASH_SERVER_PORT", portnumber.ToString());
        }

        public static bool IsServer { get; set; }

        public static string Actor {
            get {
                if (IsServer) return "server";
                return "client";
            }
        }
    }
}
