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
        const string IncludeDir = "C:\\Program Files (x86)\\Microsoft Visual Studio 11.0\\VC\\include";

        [Test]
        [Repeat(2)]
        public void HashIncludeFiles()
        {
            var ht = new HashUtil();
            var files = Directory.GetFiles(IncludeDir);
            foreach (var f in files)
            {
                var hr = ht.DigestFile(f);
                Assert.IsNotNull(hr.Hash);
            }
        }

        [Test]
        [Repeat(2)]
        public void ThreadyHashIncludeFiles()
        {
            var ht = new HashUtil();

            var hashes = ht.ThreadyDigestFiles(Directory.GetFiles(IncludeDir), true);
            Assert.IsTrue(hashes.Count > 0);
        }

        [Test]
        [Repeat(2)]
        public void HashesMatch()
        {
            var files = Directory.GetFiles(IncludeDir);
            var ht = new HashUtil();
            var hashes = ht.ThreadyDigestFiles(files, true);
            foreach (var f in files)
            {
                var hash = ht.DigestFile(f);

                if ( hash.Result ==  DataHashResult.Ok ){
                    Assert.AreEqual(hash.Hash, hashes[f].Hash);    
                }
            }

            Assert.AreEqual(files.Length, hashes.Count);

        }
    }
}
