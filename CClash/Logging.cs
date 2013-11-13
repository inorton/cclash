using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public sealed class Logging
    {
        static int pid = System.Diagnostics.Process.GetCurrentProcess().Id;

        static object miss_log_sync = new object();

        public static void Miss(DataHashResult reason, string dir, string srcfile, string headerfile) {
            if (Settings.MissLogEnabled) {
                lock (miss_log_sync) {
                    File.AppendAllLines(
                        Settings.MissLogFile,
                        new string[] {
                            string.Format("{0},dir={1},src={2},hdr={3}",
                            reason, dir, srcfile, headerfile)
                            }
                        );
                }
            }
        }

        public static void Error(string fmt, params object[] args)
        {
            Console.Error.WriteLine(fmt, args);
            Logging.Emit("error: {0}", string.Format(fmt, args));
        }

        public static void Emit(string fmt, params object[] args)
        {
            if (Settings.DebugEnabled)
            {
                for (int i = 0; i < 4; i++)
                {
                    try
                    {
                        File.AppendAllLines(Settings.DebugFile, new string[] { pid + ":" + string.Format(fmt, args) });
                        return;
                    }
                    catch {}
                }
            }
        }
    }
}
