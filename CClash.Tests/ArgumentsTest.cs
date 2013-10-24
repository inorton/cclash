using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace CClash.Tests
{
    [TestFixture]
    public class ArgumentsTest
    {
        [TestCase("\"foo\"", "bar", "baz spaces", "bar spaces")]
        [TestCase(" ", "x x x")]
        public void EscapeSpaces( params string[] argv )
        {
            Console.WriteLine(Compiler.JoinAguments(argv));
        }

        [TestCase("foo\\bar")]
        public void EscapeSlashes(params string[] argv)
        {
            Console.WriteLine(Compiler.JoinAguments(argv));
        }
    }
}
