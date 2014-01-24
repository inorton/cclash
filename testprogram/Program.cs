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
            System.Threading.Thread.Sleep(1000);

            TestProgramClients.NormalSpeedTest("inet");

            TestProgramClients.MissSpeedTest("inet");

            CClash.Program.Main(new string[] { "--cclash", "--stop" });
            Environment.SetEnvironmentVariable("CCLASH_SERVER", null);
        }
    }
}
