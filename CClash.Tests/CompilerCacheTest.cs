using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using System.Threading;

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
            Environment.SetEnvironmentVariable("CCLASH_Z7_OBJ", null);
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
        [TestCase(100)]
        public void RunEnabledDirectPdb(int times)
        {
            Assert.IsFalse(Settings.Disabled);
            Assert.IsTrue(Settings.DirectMode);
            var comp = CompilerTest.CompilerPath;
            Environment.SetEnvironmentVariable("PATH", System.IO.Path.GetDirectoryName(comp) + ";" + Environment.GetEnvironmentVariable("PATH"));
            var sw = new System.Diagnostics.Stopwatch();
            
            string pdb = string.Format("pdbtest.pdb");
            // test that the pdb gets created
            if (FileUtils.Exists(pdb)) System.IO.File.Delete(pdb);
            Assert.IsFalse(FileUtils.Exists(pdb));
            sw.Start();
            var rv = Program.Main(new string[] { "/nologo", "/Zi", "/Fd" + pdb, "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces" });
            sw.Stop();

            var duration1 = sw.Elapsed;
            Assert.IsTrue(FileUtils.Exists(pdb));
            Assert.AreEqual(0, rv);
            sw.Reset();
            sw.Start();

            for (int i = 0; i < times; i++)
            {
                
                rv = Program.Main(new string[] { "/nologo", "/Zi", "/Fd" + pdb, "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces" });
                Assert.IsTrue(FileUtils.Exists(pdb));
                Assert.AreEqual(0, rv);
            }
            sw.Stop();
            var duration2 = sw.Elapsed;

            Console.Error.WriteLine(duration1.TotalMilliseconds);
            Console.Error.WriteLine(duration2.TotalMilliseconds/times);
            
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
        public void RunEnabledDirectServerRestart(int times)
        {
            Assert.IsFalse(Settings.Disabled);
            Assert.IsTrue(Settings.DirectMode);
            var comp = CompilerTest.CompilerPath;
            Environment.SetEnvironmentVariable("CCLASH_SERVER", "yes");
            Environment.SetEnvironmentVariable("PATH", System.IO.Path.GetDirectoryName(comp) + ";" + Environment.GetEnvironmentVariable("PATH"));

            for (int i = 0; i < times; i++)
            {
                Environment.CurrentDirectory = CClashTestsFixtureSetup.InitialDir;
                var rv = Program.Main(new string[] { "/nologo", "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces" });
                Assert.IsTrue(FileUtils.Exists("hello.obj"));
                System.IO.File.Delete("hello.obj");
                Assert.AreEqual(0, rv);
                Program.Main(new string[] {"--cclash", "--stop"});
            }

        }


        public List<string> MakeLotsOfFiles(int count)
        {
            var rv = new List<string>();
            var tmp = System.IO.Path.GetTempPath();
            var root = System.IO.Path.Combine(tmp, "cclash-unit-tests");
            if (System.IO.Directory.Exists(root))
            {
                System.IO.Directory.Delete(root, true);
            }
            while (rv.Count < count)
            {
                var dn = Guid.NewGuid().ToString();
                var fn = Guid.NewGuid().ToString() + ".c";
                var folder = System.IO.Path.Combine(root, dn);
                System.IO.Directory.CreateDirectory(folder);
                var file = System.IO.Path.Combine(folder, fn);
                System.IO.File.WriteAllText(file, @"int foo(void) { return 1; }");
                rv.Add(file);
            }
            return rv;
        }

        [Test]
        [TestCase(100)]
        [TestCase(10)]
        [TestCase(1)]
        public void MPTest(int files)
        {
            var fl = MakeLotsOfFiles(files);
            var args = new List<string> { "/c", "/MP", "/nologo" };
            args.AddRange(fl);
            Assert.IsTrue(Program.Main(args.ToArray()) == 0);
        }

        [Test]
        [TestCase(10, 20, false, false)]
        [TestCase(2, 100, false, false)]
        [TestCase(200, 1, false, false)]
        [TestCase(5, 5, true, false)]
        //[TestCase(5, 5, true, true)] // TODO - pdb support is patchy, suppression is only for openssl
        public void RunEnabledDirectServerFolders(int times, int filecount, bool debug, bool pdb)
        {
            Assert.IsFalse(Settings.Disabled);
            Assert.IsTrue(Settings.DirectMode);
            var comp = CompilerTest.CompilerPath;
            Environment.SetEnvironmentVariable("CCLASH_SERVER", "1");
            Environment.SetEnvironmentVariable("CCLASH_Z7_OBJ", "yes");
            Environment.SetEnvironmentVariable("PATH", System.IO.Path.GetDirectoryName(comp) + ";" + Environment.GetEnvironmentVariable("PATH"));

            var server = new Thread(() => { Program.Main(new string[] { "--cclash-server", "--debug" }); });
            server.Start();
            try
            {
                while (Program.Server == null || !Program.Server.FirstThreadReady)
                {
                    Thread.Sleep(100);
                }
                Console.Error.WriteLine("server ready");
                var files = MakeLotsOfFiles(filecount);
                for (int i = 0; i < times; i++)
                {
                    foreach (var fn in files)
                    {
                        var dir = System.IO.Path.GetDirectoryName(fn);
                        var file = System.IO.Path.GetFileName(fn);
                        var obj = System.IO.Path.GetFileNameWithoutExtension(file) + ".obj";
                        var pdbfile = System.IO.Path.GetFileNameWithoutExtension(file) + ".pdb";
                        Environment.CurrentDirectory = dir;
                        var compargs = new List<string> { "/nologo", "/Wall", "/c", file };
                        if (debug)
                        {
                            if (pdb)
                            {
                                compargs.Add("/Zi");
                                compargs.Add("/Fd" + pdbfile);
                            }
                            else
                            {
                                compargs.Add("/Z7");
                            }
                        }
                        var rv = Program.Main(compargs.ToArray());
                        Assert.IsTrue(FileUtils.Exists(obj));
                        if (pdb)
                        {
                            // CCLASH_Z7_OBJ should suppress the pdb
                            Assert.IsFalse(FileUtils.Exists(pdbfile));
                        }
                        Assert.AreEqual(0, rv);
                    }
                }
            }
            finally
            {
                Program.Main(new string[] { "--cclash", "--stop" });
                server.Join();
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
            Environment.SetEnvironmentVariable("CCLASH_DISABLED", "1");
            Assert.IsTrue(Settings.Disabled);
            Environment.SetEnvironmentVariable("CCLASH_ENABLE_WHEN_VAR", "TESTTEST");
            Environment.SetEnvironmentVariable("CCLASH_ENABLE_WHEN_VALUES", "X,RED,GREEN");
            Environment.SetEnvironmentVariable("TESTTEST", "RED");
            Assert.IsFalse(Settings.Disabled);
            var rv = Program.Main(new string[] { "/nologo", "/c", @"test-sources\hello.c", "/Itest-sources\\inc with spaces" });
            Assert.AreEqual(0, rv);
        }
    }
}
