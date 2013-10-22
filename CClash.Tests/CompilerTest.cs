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

        public static void SetEnvs()
        {
            Environment.SetEnvironmentVariable("PATH", 
                @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\VC\bin;"
                + Environment.GetEnvironmentVariable("PATH"));
            Environment.SetEnvironmentVariable("PATH", 
                @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\IDE;"
                + Environment.GetEnvironmentVariable("PATH"));
            Environment.SetEnvironmentVariable("INCLUDE", 
                @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\VC\INCLUDE;C:\Program Files (x86)\Microsoft Visual Studio 11.0\VC\ATLMFC\INCLUDE;C:\Program Files (x86)\Windows Kits\8.0\include\shared;C:\Program Files (x86)\Windows Kits\8.0\include\um;C:\Program Files (x86)\Windows Kits\8.0\include\winrt;c:\stuff\nonsense\;c:\comp\1;c:\comp2;c:\comp3;c:\foo");

        }

        static CompilerTest()
        {
            SetEnvs();    
        }

        void EnsureDeleted(string file)
        {
            if (File.Exists(file)) 
                File.Delete(file);
        }

        [Test]
        [TestCase("/c","test-sources\\exists.c")]
        [TestCase("/c","test-sources\\exists.c", "/Fowhatever.obj")]
        [TestCase("/c","test-sources\\exists.c", "/Fotest-sources")]
        public void ParseSupportedArgs(params string[] argv)
        {
            var c = new Compiler();
            var sbo = new StringBuilder();
            var sbe = new StringBuilder();
            Assert.IsTrue(c.ProcessArguments(argv));
            Assert.IsFalse(c.Linking);
            Assert.IsTrue(c.SingleSource);
            Assert.IsNotNullOrEmpty(c.ObjectTarget);
            Assert.IsFalse(c.PrecompiledHeaders);
            Assert.AreNotEqual(c.SourceFile, c.ObjectTarget);

            EnsureDeleted(c.ObjectTarget);
            EnsureDeleted(c.PdbFile);
            
            c.CompilerExe = CompilerPath;

            var ec = c.InvokeCompiler(
                c.CommandLine,
                Console.Error.WriteLine, Console.Error.WriteLine, false, null);

            Assert.AreEqual(0, ec);

            Assert.IsTrue(File.Exists(c.ObjectTarget));
        }

        [Test]
        [TestCase("/c", "test-sources\\exists.c", "/Zi")]
        [TestCase("/c", "test-sources\\exists.c", "/Zi", "/Fowhatever.obj")]
        [TestCase("/c", "test-sources\\exists.c", "/Zi", "/Fotest-sources")]
        [TestCase("/c", "test-sources\\exists.c", "/Zi", "/Fdtest-sources\\stuff.pdb")]
        public void ParseSupportedPdbArgs(params string[] argv)
        {
            var c = new Compiler();
            c.CompilerExe = CompilerPath;
            Assert.IsTrue(c.ProcessArguments(argv));
            Assert.IsTrue(c.GeneratePdb);
            Assert.IsNotNullOrEmpty(c.PdbFile);
            EnsureDeleted(c.PdbFile);
            EnsureDeleted(c.ObjectTarget);
            var stderr = new StringBuilder();
            var stdout = new StringBuilder();
            var ec = c.InvokeCompiler(c.CommandLine,
                x => stderr.AppendLine(x),
                x => stdout.AppendLine(x), false, null);
            Assert.AreEqual(0, ec);
            Assert.IsTrue(File.Exists(c.ObjectTarget));
            Assert.IsTrue(File.Exists(c.PdbFile));
        }

        [Test]
        [TestCase("/c", "test-sources\\doesnotexist.c")]
        [TestCase("/c", "test-sources\\exists.c", "/Yu")]
        public void ParseUnSupportedArgs(params string[] argv)
        {
            var c = new Compiler();
            Assert.IsFalse(c.ProcessArguments(argv));
        }

        [Test]
        [TestCase("/c", "test-sources\\hello.c")]
        public void IncludeFileTest(params string[] argv)
        {
            var c = new Compiler() { CompilerExe = CompilerPath };
            var hv = new List<string>();

            Assert.IsTrue(c.ProcessArguments(argv));
            hv.Add(Path.GetFullPath(c.SourceFile));
            List<string> incfiles = new List<string>();
            var rv = c.InvokeCompiler(argv, x => { }, y => { }, true, incfiles);
            hv.AddRange(incfiles);
            Assert.AreEqual(0, rv);
            Assert.IsTrue(hv.Count > 0);
        }

        [Test]
        [TestCase("/c", "test-sources\\hello.c")]
        public void CompileObjectTest(params string[] argv)
        {
            var c = new Compiler() { CompilerExe = CompilerPath };
            
            Assert.IsTrue(c.ProcessArguments(argv));
            var stderr = new StringBuilder();
            var stdout = new StringBuilder();
            var rv = c.InvokeCompiler(c.CommandLine, x => stderr.AppendLine(x), x => stdout.AppendLine(x), false, null);

            Assert.AreEqual(0, rv);
        }
    }
}
