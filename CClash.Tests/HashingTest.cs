using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using System.IO;
using CClash;

namespace CClash.Tests
{
    [TestFixture]
    public class HashingTest
    {
        [Test]
        public void HashIncludeFiles()
        {
            var ht = new HashUtil();

            var incdir = "C:\\Program Files (x86)\\Microsoft Visual Studio 11.0\\VC\\include";
            foreach (var f in Directory.GetFiles(incdir))
            {
                var hr = ht.DigestFile(f);
                Assert.IsNotNull(hr.Hash);
            }
        }
    }
}
