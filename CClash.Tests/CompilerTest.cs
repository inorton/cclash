using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using NUnit.Framework;

using CClash;

namespace CClash.Tests
{
    [TestFixture]
    public class CompilerTest
    {
        public const string CompilerPath = "C:\\Program Files (x86)\\Microsoft Visual Studio 11.0\\VC\\bin\\cl.exe";
        static bool setpaths = false;

        public static string InitialDir {
            get {
                return CClashTestsFixtureSetup.InitialDir;
            }
        }

        public static void SetEnvs()
        {
            Environment.SetEnvironmentVariable("CCLASH_DISABLED", null);
            Environment.SetEnvironmentVariable("CCLASH_ATTEMPT_PDB_CACHE", null);
            Environment.SetEnvironmentVariable("CCLASH_PPMODE", null);
            Environment.SetEnvironmentVariable("CCLASH_DISABLE_WHEN_VAR", null);
            Environment.SetEnvironmentVariable("CCLASH_ENABLE_WHEN_VAR", null);
            Environment.SetEnvironmentVariable("CCLASH_SLOWOBJ_TIMEOUT", "3000");
            if (!setpaths)
            {
                Environment.SetEnvironmentVariable("PATH",
                    @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\VC\bin;"
                    + Environment.GetEnvironmentVariable("PATH"));
                Environment.SetEnvironmentVariable("PATH",
                    @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\IDE;"
                    + Environment.GetEnvironmentVariable("PATH"));
                Environment.SetEnvironmentVariable("INCLUDE",
                    @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\VC\INCLUDE;C:\Program Files (x86)\Microsoft Visual Studio 11.0\VC\ATLMFC\INCLUDE;C:\Program Files (x86)\Windows Kits\8.0\include\shared;C:\Program Files (x86)\Windows Kits\8.0\include\um;C:\Program Files (x86)\Windows Kits\8.0\include\winrt;c:\stuff\nonsense\;c:\comp\1;c:\comp2;c:\comp3;c:\foo");
                setpaths = true;
            }
        }

        [SetUp]
        public void Init()
        {
            SetEnvs();
            Environment.CurrentDirectory = "c:\\";
        }

        [TearDown]
        public void Down()
        {
            SetEnvs();
            Environment.CurrentDirectory = "c:\\";
        }

        void EnsureDeleted(string file)
        {
            if ( !string.IsNullOrEmpty(file) && FileUtils.Exists(file)) 
                File.Delete(file);
        }

        [Test]
        [TestCase("exists.obj", "/c", "test-sources\\exists.c")]
        [TestCase("whatever.obj", "/c", "test-sources\\exists.c", "/Fowhatever.obj")]
        [TestCase("test-sources.obj", "/c", "test-sources\\exists.c", "/Fotest-sources")]
        [TestCase("test-sources\\exists.obj", "/c", "test-sources\\exists.c", "/Fotest-sources\\")]
        public void CompilerArgumentParsing(string objfile, params string[] argv)
        {
            var c = new Compiler();
            c.SetWorkingDirectory(InitialDir);
            c.SetEnvironment(Compiler.GetEnvironmentDictionary());
            c.SetWaitForSlowObject(3000);
            var sbo = new StringBuilder();
            var sbe = new StringBuilder();
            Assert.IsTrue(c.ProcessArguments(argv));
            Assert.IsFalse(c.Linking);
            Assert.IsTrue(c.SingleSource);
            Assert.IsNotNullOrEmpty(c.ObjectTarget);
            Assert.IsFalse(c.PrecompiledHeaders);
            Assert.AreNotEqual(c.SingleSourceFile, c.ObjectTarget);
            Assert.AreEqual(Path.Combine(InitialDir, objfile), c.ObjectTarget);

            EnsureDeleted(c.ObjectTarget);
            EnsureDeleted(c.PdbFile);

            c.CompilerExe = CompilerPath;
            c.SetWorkingDirectory(InitialDir);
            c.SetEnvironment(Compiler.GetEnvironmentDictionary());

            var stderr = new StringBuilder();
            var stdout = new StringBuilder();

            var ec = c.InvokeCompiler(
                c.CommandLine,
                (x) => { stderr.AppendLine(x); },
                (x) => { stdout.AppendLine(x); },
                false, null);

            Assert.AreEqual(0, ec);

            Assert.IsTrue(File.Exists(c.ObjectTarget));
        }

        [Test]
        [TestCase("/c", "test-sources\\exists.c", "/Zi")]
        [TestCase("/c", "test-sources\\exists.c", "/Zi", "/Fowhatever.obj")]
        [TestCase("/c", "test-sources\\exists.c", "/Zi", "/Fotest-sources")]
        public void ParseUnSupportedPdbArgs(params string[] argv)
        {
            var c = new Compiler();
            c.SetWorkingDirectory(InitialDir);
            c.CompilerExe = CompilerPath;
            Assert.IsFalse(c.ProcessArguments(argv));
        }

        [TestCase("/c", "test-sources\\exists.c", "/Zi", "/Fdtest-sources\\stuff.pdb")]
        public void ParseSupportedPdbArgs(params string[] argv)
        {
            Assert.IsFalse(Settings.AttemptPDBCaching);
            var c = new Compiler();
            c.SetWorkingDirectory(InitialDir);
            c.CompilerExe = CompilerPath;
            Assert.IsFalse(c.ProcessArguments(argv));
        }


        [Test]
        [TestCase("/c", "test-sources\\exists.c", "/Zi","/Z7")]
        [TestCase("/c", "test-sources\\exists.c", "/Zi", "/Fowhatever.obj", "/Z7")]
        [TestCase("/c", "test-sources\\exists.c", "/Zi", "/Fotest-sources", "/Z7")]
        [TestCase("/c", "test-sources\\exists.c", "/Zi", "/Fdtest-sources\\stuff.pdb", "/Z7")]
        public void ParseSupportedDebugArgs(params string[] argv) {
            var c = new Compiler();
            c.SetWorkingDirectory(InitialDir);
            c.CompilerExe = CompilerPath;
            Assert.IsTrue(c.ProcessArguments(argv));
            Assert.IsFalse(c.GeneratePdb);
        }

        [Test]
        [TestCase("/c", "test-sources\\doesnotexist.c")]
        [TestCase("/c", "test-sources\\exists.c", "/Yu")]
        [TestCase("/c", "test-sources\\exists.c", "/Zi")]
        [TestCase("/c", "test-sources\\exists.c", "/Zi", "/Fowhatever.obj")]
        public void ParseUnSupportedArgs(params string[] argv)
        {
            var c = new Compiler();
            c.SetWorkingDirectory(InitialDir);
            Assert.IsFalse(c.ProcessArguments(argv));
        }

        [Test]
        [TestCase("/c", "test-sources\\hello.c", "/Itest-sources\\inc with spaces")]
        public void IncludeFileTest(params string[] argv)
        {
            var c = new Compiler() { CompilerExe = CompilerPath };
            var hv = new List<string>();
            c.SetWorkingDirectory(InitialDir);
            c.SetEnvironment(Compiler.GetEnvironmentDictionary());
            Assert.IsTrue(c.ProcessArguments(argv));
            hv.Add(Path.GetFullPath(c.SingleSourceFile));
            List<string> incfiles = new List<string>();
            var rv = c.InvokeCompiler(argv, Console.Error.WriteLine, Console.Out.WriteLine, true, incfiles);
            hv.AddRange(incfiles);
            Assert.AreEqual(0, rv);
            Assert.IsTrue(hv.Count > 0);
        }

        [Test]
        [TestCase("/c", "test-sources\\hello.c", "/Itest-sources\\inc with spaces")]
        [TestCase("/c", "test-sources\\hello.c", "/I", "test-sources\\inc with spaces", "/D", "a_hash_define")]
        [TestCase("@test-sources\\compiler1.resp")]
        public void PreprocessorTest(params string[] argv)
        {
            var c = new Compiler() { CompilerExe = CompilerPath };
            c.SetWorkingDirectory(InitialDir);
            c.SetEnvironment(Compiler.GetEnvironmentDictionary());

            var supported = c.ProcessArguments(argv);
            
            Assert.IsTrue(supported);
            Assert.AreEqual(1, c.CliIncludePaths.Count);
            Assert.AreEqual( Path.Combine(InitialDir, "test-sources\\inc with spaces"), c.CliIncludePaths[0]);
            Assert.AreEqual( Path.Combine(InitialDir, "test-sources\\hello.c"), c.SingleSourceFile);
            using (var sw = new StreamWriter(new MemoryStream()))
            {
                var rv = c.InvokePreprocessor(sw);
                Assert.AreEqual(0, rv);
            }
        }

        [Test]
        [TestCase("/c", "test-sources\\hello.c", "/Itest-sources\\inc with spaces")]
        [TestCase("/c", "test-sources\\hello.c", "/I","test-sources\\inc with spaces","/D","a_hash_define")]
        [TestCase("@test-sources\\compiler1.resp")]
        public void CompileObjectTest(params string[] argv)
        {
            var c = new Compiler() { CompilerExe = CompilerPath };
            c.SetWorkingDirectory(InitialDir);
            c.SetEnvironment(Compiler.GetEnvironmentDictionary());
            
            Assert.IsTrue(c.ProcessArguments(argv));
            Assert.AreEqual(1, c.CliIncludePaths.Count);
            Assert.AreEqual( Path.Combine(InitialDir, "test-sources\\inc with spaces"), c.CliIncludePaths[0]);
            Assert.AreEqual( Path.Combine(InitialDir, "test-sources\\hello.c"), c.SingleSourceFile);
            var stderr = new StringBuilder();
            var stdout = new StringBuilder();
            var rv = c.InvokeCompiler(c.CommandLine, x => stderr.AppendLine(x), x => stdout.AppendLine(x), false, null);

            Assert.AreEqual(0, rv);
        }

        [Test]
        [TestCase("/o","foo.exe")]
        [TestCase("/c","test-sources\\exists.c","test-sources\\hello.c")]
        [TestCase("/link")]
        public void DetectNotSupported(params string[] argv)
        {
            var c = new Compiler() { 
                CompilerExe = CompilerPath };
            c.SetWorkingDirectory(InitialDir);
            c.SetEnvironment(Compiler.GetEnvironmentDictionary());
            Assert.IsFalse(c.ProcessArguments(argv));
        }
    }
}
