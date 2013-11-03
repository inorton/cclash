using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            Environment.SetEnvironmentVariable("CCLASH_HARDLINK", "yes");
            Environment.SetEnvironmentVariable("CCLASH_SERVER", "yes");
            var t = new CompilerCacheTest();
            t.Init();
            
            Environment.SetEnvironmentVariable("CCLASH_DEBUG", "test.log");
            var times = 100;
            var start = DateTime.Now;
            t.RunEnabledDirect(times);
            var end = DateTime.Now;

            var duration = end.Subtract(start);

            Console.WriteLine("{0} operations in {1} sec. {2}/ops, {3}ms/op",
                times, duration.TotalSeconds, times / duration.TotalSeconds, duration.TotalMilliseconds / times);
            Console.ReadLine();
        }
    }
}
