using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CClash
{
    public sealed class Logging
    {
        static int pid = System.Diagnostics.Process.GetCurrentProcess().Id;

        static object miss_log_sync = new object();

        public static void Miss( string hc, DataHashResult reason, string dir, string srcfile, string headerfile)
        {
            switch (reason)
            {
                case DataHashResult.FileNotFound:
                    break;
                default:
                    break;
            }

            HitMissRecord(reason.ToString() + " hc=" + hc, dir, srcfile, headerfile);
        }

        public static void Hit( string hashkey, string dir, string obj)
        {
            if (Settings.DebugEnabled)
            {
                AppendMissLog(string.Format("HIT {0},dir={1},obj={2}", hashkey, dir, obj));
            }
        }

        static void AppendMissLog(string str)
        {
            if (Settings.MissLogEnabled)
            {
                lock (miss_log_sync)
                {
                    File.AppendAllText(Settings.MissLogFile, DateTime.Now.ToString("s") + str + Environment.NewLine);
                }
            }
        }

        static void HitMissRecord(string reason, string dir, string srcfile, string headerfile) {
            AppendMissLog(string.Format(" {0},dir={1},src={2},hdr={3}",
                            reason, dir, srcfile, headerfile));
        }

        public static void Warning(string fmt, params object[] args)
        {
            Console.Error.WriteLine(fmt, args);
            Logging.Emit("warning: {0}", string.Format(fmt, args));
        }

        public static void Input(string dir, string target, IEnumerable<string> args)
        {
            if (Settings.DebugEnabled)
            {
                var cfiles = from a in args where a.Contains(".c") select a;
                Logging.Emit("invoked: dir={0}, target={1} srcs={2}", dir, target, string.Join(",", cfiles.ToArray()));
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
                        if (Settings.DebugFile == "Console") {
                            Console.Error.WriteLine("p{0} t{1}:{2}", pid, 
                                Thread.CurrentThread.ManagedThreadId, 
                                string.Format(fmt, args));
                        } else {
                            File.AppendAllLines(Settings.DebugFile, new string[] { pid + ":" + string.Format(fmt, args) });
                        }
                        return;
                    }
                    catch {}
                }
            }
        }
    }
}
