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
        static bool clearCache = false;

        [SetUp]
        public void Init()
        {
            CompilerTest.SetEnvs();
            if (!clearCache)
            {
                Environment.SetEnvironmentVariable("CCLASH_DIR", "compilercache-tests");
                clearCache = true;
                try
                {
                    if (System.IO.Directory.Exists("compilercache-tests"))
                        System.IO.Directory.Delete("compilercache-tests", true);
                }
                catch { }
            }
            Environment.SetEnvironmentVariable("CCLASH_DEBUG", null);
            Environment.SetEnvironmentVariable("CCLASH_SERVER", null);
            Environment.SetEnvironmentVariable("CCLASH_HARDLINK", null);
        }

        [TearDown]
        public void Down()
        {
            Init();
            Environment.SetEnvironmentVariable("CCLASH_DIR", null);
        }

        [Test]
        public void Run0GetOverhead()
        {
            Assert.IsFalse(Settings.Disabled);
        }


        [Test]
        [TestCase(10)]
        public void RunEnabledDirect(int times)
        {
            Assert.IsFalse(Settings.Disabled);
            Assert.IsTrue(Settings.DirectMode);
            var comp = CompilerTest.CompilerPath;
            Environment.SetEnvironmentVariable("PATH", System.IO.Path.GetDirectoryName( comp ) + ";" + Environment.GetEnvironmentVariable("PATH"));
            for (int i = 0; i < times; i++)
            {
                var rv = Program.Main(new string[] { "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces" });
                Assert.AreEqual(0, rv);
            }
        }

        [Test]
        [TestCase(10)]
        public void RunEnabledDirectHardLink(int times)
        {
            Assert.IsFalse(Settings.Disabled);
            Assert.IsTrue(Settings.DirectMode);
            var comp = CompilerTest.CompilerPath;
            Environment.SetEnvironmentVariable("CCLASH_HARDLINK", "yes");
            Assert.IsTrue(Settings.UseHardLinks);
            Environment.SetEnvironmentVariable("PATH", System.IO.Path.GetDirectoryName(comp) + ";" + Environment.GetEnvironmentVariable("PATH"));
            for (int i = 0; i < times; i++)
            {
                var rv = Program.Main(new string[] { "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces" });
                Assert.AreEqual(0, rv);
            }
        }

        [Test]
        [TestCase(10, null)]
        [TestCase(10, "ppmode.log")]
        public void RunEnabledPPMode(int times, string log)
        {
            Assert.IsFalse(Settings.Disabled);
            Assert.IsTrue(Settings.DirectMode);
            Environment.SetEnvironmentVariable("CCLASH_PPMODE", "yes");
            Environment.SetEnvironmentVariable("CCLASH_DEBUG", log);
            Assert.IsTrue(Settings.PreprocessorMode);
            var comp = CompilerTest.CompilerPath;
            var sw = DateTime.Now;
            Environment.SetEnvironmentVariable("PATH", System.IO.Path.GetDirectoryName(comp) + ";" + Environment.GetEnvironmentVariable("PATH"));
            for (int i = 0; i < times; i++)
            {
                var rv = Program.Main(new string[] { "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces" });
                Assert.AreEqual(0, rv);
            }
            var ew = DateTime.Now;
            Console.Error.WriteLine(ew.Subtract(sw).TotalMilliseconds);
        }

        [Test]
        [TestCase(10)]
        public void RunCacheMissPPMode(int times)
        {
            Assert.IsFalse(Settings.Disabled);
            Assert.IsTrue(Settings.DirectMode);
            Environment.SetEnvironmentVariable("CCLASH_PPMODE", "yes");
            Assert.IsTrue(Settings.PreprocessorMode);
            var comp = CompilerTest.CompilerPath;
            Environment.SetEnvironmentVariable("PATH", System.IO.Path.GetDirectoryName(comp) + ";" + Environment.GetEnvironmentVariable("PATH"));
            var sw = DateTime.Now;
            for (int i = 0; i < times; i++)
            {
                var g = Guid.NewGuid().ToString().Substring(0, 5);
                var rv = Program.Main(new string[] { "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces", "/DRANDOM=" + g });
                Assert.AreEqual(0, rv);
            }
            var ew = DateTime.Now;
            Console.Error.WriteLine(ew.Subtract(sw).TotalMilliseconds);
        }

        [Test]
        [TestCase(10)]
        public void RunDisabled(int times)
        {
            Assert.IsFalse(Settings.Disabled);
            Environment.SetEnvironmentVariable("CCLASH_DISABLED", "1");
            Assert.IsTrue(Settings.Disabled);
            for (int i = 0; i < times; i++)
            {
                var rv = Program.Main(new string[] { "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces" });
                Assert.AreEqual(0, rv);
            }
        }

        [Test]
        [Repeat(2)]
        public void RunDisabledByCondition()
        {
            Assert.IsFalse(Settings.Disabled);
            Environment.SetEnvironmentVariable("CCLASH_DISABLE_WHEN_VAR", "TESTTEST");
            Environment.SetEnvironmentVariable("CCLASH_DISABLE_WHEN_VALUES", "X,RED,GREEN");
            Environment.SetEnvironmentVariable("TESTTEST", "RED");
            Assert.IsTrue(Settings.Disabled);
            var rv = Program.Main(new string[] { "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces" });
            Assert.AreEqual(0, rv);
        }


        [Test]
        [Repeat(2)]
        public void RunEnabledByCondition()
        {
            Assert.IsFalse(Settings.Disabled);
            Environment.SetEnvironmentVariable("CCLASH_ENABLE_WHEN_VAR", "TESTTEST");
            Environment.SetEnvironmentVariable("CCLASH_ENABLE_WHEN_VALUES", "X,RED,GREEN");
            Environment.SetEnvironmentVariable("TESTTEST", "RED");
            Assert.IsFalse(Settings.Disabled);
            var rv = Program.Main(new string[] { "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces" });
            Assert.AreEqual(0, rv);
        }
    }
}
