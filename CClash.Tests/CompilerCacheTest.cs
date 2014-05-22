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
        public const string CacheFolderName = "cclash-unittest";
        public static string cachesuffix = null;

        [SetUp]
        public void Init()
        {
            if ( cachesuffix == null ) {
                cachesuffix = Guid.NewGuid().ToString().Substring(0, 5);
            }
            CompilerTest.SetEnvs();
            
            Environment.SetEnvironmentVariable("CCLASH_DEBUG", null);
            Environment.SetEnvironmentVariable("CCLASH_SERVER", null);
            Environment.SetEnvironmentVariable("CCLASH_HARDLINK", null);
            Environment.SetEnvironmentVariable("CCLASH_DIR", System.IO.Path.Combine(CClashTestsFixtureSetup.InitialDir, CacheFolderName + cachesuffix));
            Environment.CurrentDirectory = CClashTestsFixtureSetup.InitialDir;
        }

        [TearDown]
        public void Down()
        {
            Environment.SetEnvironmentVariable("CCLASH_DIR", null);
            Environment.CurrentDirectory = "c:\\";
            if (System.IO.Directory.Exists(System.IO.Path.Combine( CClashTestsFixtureSetup.InitialDir, CacheFolderName)))
            {
                System.IO.Directory.Delete(System.IO.Path.Combine(CClashTestsFixtureSetup.InitialDir, CacheFolderName), true);
            }
                        
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
                var rv = Program.Main(new string[] { "/nologo", "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces" });
                Assert.AreEqual(0, rv);
            }
        }

        [Test]
        [TestCase(10)]
        public void RunEnabledDirectServer(int times)
        {
            Assert.IsFalse(Settings.Disabled);
            Assert.IsTrue(Settings.DirectMode);
            var comp = CompilerTest.CompilerPath;
            Environment.SetEnvironmentVariable("CCLASH_SERVER", "yes");
            Environment.SetEnvironmentVariable("PATH", System.IO.Path.GetDirectoryName(comp) + ";" + Environment.GetEnvironmentVariable("PATH"));
            for (int i = 0; i < times; i++)
            {
                var rv = Program.Main(new string[] { "/nologo", "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces" });
                Assert.AreEqual(0, rv);
            }
        }

        [Test]
        [TestCase(100)]
        public void RunDisabled(int times)
        {
            Assert.IsFalse(Settings.Disabled);
            Environment.SetEnvironmentVariable("CCLASH_DISABLED", "1");
            Assert.IsTrue(Settings.Disabled);
            for (int i = 0; i < times; i++)
            {
                Console.Error.WriteLine("{0}/{1}", i, times);
                var rv = Program.Main(new string[] { "/nologo", "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces" });
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
            var rv = Program.Main(new string[] { "/nologo", "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces" });
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
            var rv = Program.Main(new string[] { "/nologo", "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces" });
            Assert.AreEqual(0, rv);
        }
    }
}
