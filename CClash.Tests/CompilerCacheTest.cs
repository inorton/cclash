using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace CClash.Tests
{
    [TestFixture]
    public class CompilerCacheTest
    {
        [Test]
        public void RunEnabled()
        {
            var comp = CompilerTest.CompilerPath;
            CompilerTest.SetEnvs();
            Environment.SetEnvironmentVariable("CCLASH_DISABLED", null);
            Environment.SetEnvironmentVariable("PATH", System.IO.Path.GetDirectoryName( comp ) + ";" + Environment.GetEnvironmentVariable("PATH"));

            var rv = Program.Main(new string[] { "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces" });
            Assert.AreEqual(0, rv);
        }

        [Test]
        public void RunEnabledForceBuild()
        {
            var comp = CompilerTest.CompilerPath;
            var tmpcache = System.IO.Path.Combine(Environment.CurrentDirectory, "clean");
            try
            {
                CompilerTest.SetEnvs();
                if (System.IO.Directory.Exists(tmpcache)) System.IO.Directory.CreateDirectory(tmpcache);
                Environment.SetEnvironmentVariable("CCLASH_DIR", "clean");
                Environment.SetEnvironmentVariable("CCLASH_DISABLED", null);
                Environment.SetEnvironmentVariable("PATH", System.IO.Path.GetDirectoryName(comp) + ";" + Environment.GetEnvironmentVariable("PATH"));

                var rv = Program.Main(new string[] { "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces" });
                Assert.AreEqual(0, rv);
            }
            finally
            {
                if (System.IO.Directory.Exists(tmpcache)) System.IO.Directory.Delete(tmpcache, true);
            }
        }

        [Test]
        public void RunDisabled()
        {
            CompilerTest.SetEnvs();
            Environment.SetEnvironmentVariable("CCLASH_DISABLED", "1");

            var rv = Program.Main(new string[] { "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces" });
            Assert.AreEqual(0, rv);
        }
    }
}
