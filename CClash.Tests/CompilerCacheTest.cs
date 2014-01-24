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
        public const string CacheFolderNamePrefix = "cclash-unittest";
        public const string OutDirPrefix = "cclash-objs";

        static string CacheFolderName;
        static string OutDir;

        [SetUp]
        public void Init()
        {
            CompilerTest.SetEnvs();

            CacheFolderName = String.Format("{0}_{1}", CacheFolderNamePrefix, Guid.NewGuid().ToString().Substring(0, 6));
            OutDir = String.Format("{0}_{1}", OutDirPrefix, Guid.NewGuid().ToString().Substring(0, 6));
            
            Environment.SetEnvironmentVariable("CCLASH_DEBUG", null);
            Environment.SetEnvironmentVariable("CCLASH_SERVER", null);
            Environment.SetEnvironmentVariable("CCLASH_HARDLINK", null);

            Environment.SetEnvironmentVariable("CCLASH_DIR", CacheFolderName);
        }

        [TearDown]
        public void Down()
        {
            Environment.SetEnvironmentVariable("CCLASH_DIR", null);
            Environment.SetEnvironmentVariable("CCLASH_SERVER", null);
            if (System.IO.Directory.Exists(CacheFolderName))
            {
                int attemps = 4;
            retry:
                try
                {
                    System.IO.Directory.Delete(CacheFolderName, true);
                    System.IO.Directory.Delete(OutDir, true);
                }
                catch (System.IO.DirectoryNotFoundException)
                {
                }
                catch (Exception)
                {
                    if (attemps-- > 0)
                        goto retry;
                    throw;
                }
            }
                
        }

        public static string MakeOutfileArg()
        {
            if (!System.IO.Directory.Exists(OutDir))
                System.IO.Directory.CreateDirectory(OutDir);
            var fo = string.Format("/Fo{0}.obj", System.IO.Path.Combine( OutDir, Guid.NewGuid().ToString().Substring(0, 5)) );
            return fo;
        }

        [Test]
        public void Run0GetOverhead()
        {
            Assert.IsFalse(Settings.Disabled);
        }


        [Test]
        [TestCase(100)]
        public void RunEnabledDirectForcedMiss(int times)
        {
            Assert.IsFalse(Settings.Disabled);
            Assert.IsTrue(Settings.DirectMode);
            var comp = CompilerTest.CompilerPath;
            Environment.SetEnvironmentVariable("PATH", System.IO.Path.GetDirectoryName(comp) + ";" + Environment.GetEnvironmentVariable("PATH"));
            for (int i = 0; i < times; i++)
            {
                Console.Error.WriteLine("# {0}", i);
                var srcfile = System.IO.Path.Combine( "test-sources", Guid.NewGuid().ToString() + ".c" );
                var fo = MakeOutfileArg();
                System.IO.File.Copy( @"test-sources\hello.c", srcfile, true );
                var rv = Program.Main(new string[] { "/nologo", "/c", srcfile, fo , "/Itest-sources\\inc with spaces" });
                Assert.AreEqual(0, rv);
            }
        }

        [Test]
        [TestCase(500)]
        public void RunEnabledDirect(int times)
        {
            Assert.IsFalse(Settings.Disabled);
            Assert.IsTrue(Settings.DirectMode);
            var comp = CompilerTest.CompilerPath;
            var fo = MakeOutfileArg();
            Environment.SetEnvironmentVariable("PATH", System.IO.Path.GetDirectoryName( comp ) + ";" + Environment.GetEnvironmentVariable("PATH"));
            for (int i = 0; i < times; i++)
            {
                Console.Error.WriteLine("# {0}", i);
                var rv = Program.Main(new string[] { "/nologo", "/c", @"test-sources\hello.c", fo, "/Itest-sources\\inc with spaces" });
                Assert.AreEqual(0, rv);
            }
        }

        [Test]
        [Explicit]
        [TestCase(500, "pipe")]
        [TestCase(500, "inet")]
        public void RunEnabledDirectServer(int times, string srvmode)
        {
            Assert.IsFalse(Settings.Disabled);
            Assert.IsTrue(Settings.DirectMode);
            var comp = CompilerTest.CompilerPath;
            var fo = MakeOutfileArg();
            Environment.SetEnvironmentVariable("CCLASH_SERVER", srvmode);
            Environment.SetEnvironmentVariable("PATH", System.IO.Path.GetDirectoryName(comp) + ";" + Environment.GetEnvironmentVariable("PATH"));
            for (int i = 0; i < times; i++)
            {
                Console.Error.WriteLine("run {0}/{1}", i+1, times);
                var rv = Program.Main(new string[] { "/nologo", "/c", @"test-sources\hello.c", fo, "/Itest-sources\\inc with spaces" });
                Assert.AreEqual(0, rv);
            }
            Program.Main(new string[] { "--cclash", "--stop" });
            

        }

        [Test]
        [TestCase(500)]
        public void RunDisabled(int times)
        {
            Assert.IsFalse(Settings.Disabled);
            Environment.SetEnvironmentVariable("CCLASH_DISABLED", "1");
            Assert.IsTrue(Settings.Disabled);
            for (int i = 0; i < times; i++)
            {
                var fo = MakeOutfileArg();
                var rv = Program.Main(new string[] { "/nologo", "/c", @"test-sources\hello.c", fo, "/Itest-sources\\inc with spaces" });
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
            var fo = MakeOutfileArg();
            var rv = Program.Main(new string[] { "/nologo", "/c", @"test-sources\hello.c", fo, "/Itest-sources\\inc with spaces" });
            Assert.AreEqual(0, rv);
        }

        [Test]
        [Repeat(1000)]
        public void CheckDisabledByCondition()
        {
            Assert.IsFalse(Settings.Disabled);
            Environment.SetEnvironmentVariable("CCLASH_DISABLE_WHEN_VAR", "TESTTEST");
            Environment.SetEnvironmentVariable("CCLASH_DISABLE_WHEN_VALUES", "X,RED,GREEN");
            Environment.SetEnvironmentVariable("TESTTEST", "RED");
            Assert.IsTrue(Settings.Disabled);
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
            var fo = MakeOutfileArg();
            var rv = Program.Main(new string[] { "/nologo", "/c", @"test-sources\hello.c", fo, "/Itest-sources\\inc with spaces" });
            Assert.AreEqual(0, rv);
        }

        [Test]
        [Repeat(1000)]
        public void CheckEnabledByCondition()
        {
            Assert.IsFalse(Settings.Disabled);
            Environment.SetEnvironmentVariable("CCLASH_ENABLE_WHEN_VAR", "TESTTEST");
            Environment.SetEnvironmentVariable("CCLASH_ENABLE_WHEN_VALUES", "X,RED,GREEN");
            Environment.SetEnvironmentVariable("TESTTEST", "RED");
            Assert.IsFalse(Settings.Disabled);
        }
    }
}
