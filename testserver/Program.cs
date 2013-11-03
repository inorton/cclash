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
            Environment.SetEnvironmentVariable("CCLASH_HARDLINK", "yes");
            Environment.SetEnvironmentVariable("CCLASH_SERVER", "yes");
            Environment.SetEnvironmentVariable("CCLASH_DIR", "compilercache-tests");
            var serv = new CClashServer();
            serv.Listen(Settings.CacheDirectory);

        }
    }
}
