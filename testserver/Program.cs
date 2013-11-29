using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    class Program
    {
        static void Main(string[] args)
        {
            Environment.SetEnvironmentVariable("CCLASH_TRY_HARDLINKS", "yes");
            Environment.SetEnvironmentVariable("CCLASH_SERVER", "yes");
            Environment.SetEnvironmentVariable("CCLASH_LAZY_NEW_INCLUDES", "yes");
            Environment.SetEnvironmentVariable("CCLASH_MISSES", System.IO.Path.Combine( Environment.CurrentDirectory, "misses.txt"));
            Settings.CacheDirectory = System.IO.Path.Combine( Environment.CurrentDirectory, "cclash-unittest");

            try
            {
                if (System.IO.Directory.Exists(Settings.CacheDirectory))
                    System.IO.Directory.Delete(Settings.CacheDirectory, true);
            }
            catch { }
            var serv = new CClashServer();
            serv.Listen(Settings.CacheDirectory);
            Console.Error.WriteLine("finished..");
            Console.ReadLine();
        }
    }
}
