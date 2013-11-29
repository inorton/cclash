using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash.Tests
{
    public class TestProgramClients
    {
        public static void NormalSpeedTest()
        {
            var t = new CompilerCacheTest();
            t.Init();
            Settings.CacheDirectory = System.IO.Path.Combine(Environment.CurrentDirectory, "cclash-unittest");
            var times = 500;
            var start = DateTime.Now;

            Environment.SetEnvironmentVariable("CCLASH_TRY_HARDLINKS", "yes");
            Environment.SetEnvironmentVariable("CCLASH_SERVER", "yes");
            CClash.Program.Main(new string[] { "--cclash" });
            t.RunEnabledDirect(times);

            var end = DateTime.Now;
            Logging.Miss(DataHashResult.NoPreviousBuild, "test", "test", "test");
            var duration = end.Subtract(start);

            Console.WriteLine("{0} operations in {1} sec. {2}/ops, {3}ms/op",
                times, duration.TotalSeconds, times / duration.TotalSeconds, duration.TotalMilliseconds / times);
            CClash.Program.Main(new string[] { "--cclash" });
            Console.ReadLine();
        }

        public static void MissSpeedTest()
        {
            var t = new CompilerCacheTest();
            t.Init();
            Settings.CacheDirectory = System.IO.Path.Combine(Environment.CurrentDirectory, "cclash-unittest");
            var times = 500;
            var start = DateTime.Now;

            Environment.SetEnvironmentVariable("CCLASH_TRY_HARDLINKS", "yes");
            Environment.SetEnvironmentVariable("CCLASH_SERVER", "yes");
            CClash.Program.Main(new string[] { "--cclash" });
            t.RunEnabledDirectForcedMiss(times);

            var end = DateTime.Now;
            Logging.Miss(DataHashResult.NoPreviousBuild, "test", "test", "test");
            var duration = end.Subtract(start);

            Console.WriteLine("{0} operations in {1} sec. {2}/ops, {3}ms/op",
                times, duration.TotalSeconds, times / duration.TotalSeconds, duration.TotalMilliseconds / times);
            CClash.Program.Main(new string[] { "--cclash" });
            Console.ReadLine();
        }
    }
}
