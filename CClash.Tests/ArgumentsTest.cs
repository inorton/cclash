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
        [Test]
        public void EscapeSpaces()
        {
            Assert.AreEqual("\"foo\" bar \"baz spaces\" \"bar spaces\"",
                ArgumentUtils.JoinAguments(new string[] { "\"foo\"", "bar", "baz spaces", "bar spaces" }));

            Assert.AreEqual("\" \" \"x x x\"",
                ArgumentUtils.JoinAguments(new string[] { " ", "x x x" }));
        }

        [Test]
        public void EscapeSlashes()
        {
            Assert.AreEqual("foo\\bar\\file.c",
                ArgumentUtils.JoinAguments(new string[] { "foo\\bar\\file.c" }));
        }

        [Test]
        public void EscapeQuotes()
        {
            Assert.AreEqual(new string[] {"/D"}, 
                ArgumentUtils.FixupArgs(new string[]{"/D"}));
        }


    }
}
