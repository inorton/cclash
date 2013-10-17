using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

using CClash;

namespace CClash.Tests
{
    [TestFixture]
    public class CompilerTest
    {

        [Test]
        [TestCase( "/c","test-sources\\exists.c" )]
        public void ParseSupportedArgs(params string[] argv)
        {
            var c = new Compiler();
            Assert.IsTrue( c.ProcessArguments(argv) );
        }

        [Test]
        [TestCase( "/c", "test-sources\\doesnotexist.c" )]
        public void ParseUnSupportedArgs(params string[] argv)
        {
            var c = new Compiler();
            Assert.IsFalse(c.ProcessArguments(argv));
        }
    }
}
