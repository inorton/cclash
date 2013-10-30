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
            var t = new CompilerCacheTest();
            t.Init();
            t.RunEnabledPPMode(100, null);
        }
    }
}
