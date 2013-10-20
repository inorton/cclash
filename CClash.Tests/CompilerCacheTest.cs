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

            var rv = Program.Main( new string[] { "/c", @"test-sources\hello.c" });
            Assert.AreEqual(0, rv);
        }

        [Test]
        public void RunDisabled()
        {
            CompilerTest.SetEnvs();
            Environment.SetEnvironmentVariable("CCLASH_DISABLED", "1");

            var rv = Program.Main(new string[] { "/c", @"test-sources\hello.c" });
            Assert.AreEqual(0, rv);
        }
    }
}
