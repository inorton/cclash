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
        [Repeat(10)]
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
        [Repeat(10)]
        public void RunDisabled()
        {
            CompilerTest.SetEnvs();
            Environment.SetEnvironmentVariable("CCLASH_DISABLED", "1");
            Assert.IsTrue(Settings.Disabled);
            var rv = Program.Main(new string[] { "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces" });
            Assert.AreEqual(0, rv);
        }

        [Test]
        [Repeat(10)]
        public void RunDisabledByCondition()
        {
            CompilerTest.SetEnvs();
            Environment.SetEnvironmentVariable("CCLASH_DISABLED", null);
            Environment.SetEnvironmentVariable("CCLASH_DISABLE_WHEN_VAR", null);
            Assert.IsFalse(Settings.Disabled);
            Environment.SetEnvironmentVariable("CCLASH_DISABLE_WHEN_VAR", "TESTTEST");
            Environment.SetEnvironmentVariable("CCLASH_DISABLE_WHEN_VALUES", "X,RED,GREEN");
            Environment.SetEnvironmentVariable("TESTTEST", "RED");
            Assert.IsTrue(Settings.Disabled);
            var rv = Program.Main(new string[] { "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces" });
            Assert.AreEqual(0, rv);
        }
    }
}
