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
        const string CompilerPath = "C:\\Program Files (x86)\\Microsoft Visual Studio 11.0\\VC\\bin\\cl.exe";

        static CompilerTest()
        {
            Environment.SetEnvironmentVariable("PATH", "C:\\Program Files (x86)\\Microsoft Visual Studio 11.0\\VC\\bin;"
                + Environment.GetEnvironmentVariable("PATH"));
            Environment.SetEnvironmentVariable("PATH", "C:\\Program Files (x86)\\Microsoft Visual Studio 11.0\\Common7\\IDE;"
                + Environment.GetEnvironmentVariable("PATH"));
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
                sbe, sbo);
            Console.Error.WriteLine(sbe.ToString());
            Console.Error.WriteLine(sbo.ToString());
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
            var ec = c.InvokeCompiler(c.CommandLine, stderr, stdout);
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
    }
}
