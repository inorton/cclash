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
        public void TestExists() 
        {
            var files = Directory.GetFiles(IncludeDir);
            foreach (var f in files)
                Assert.IsTrue(FileUtils.Exists(f));
        }

        [Test]
        [Repeat(2)]
        [TestCase(1)]
        [TestCase(4)]
        public void HashIncludeFiles(int threads)
        {
            using (var ic = FileCacheStore.Load("testincs"))
            {
                var ht = new HashUtil(ic);
                ht.HashingThreadCount = threads;
                var files = Directory.GetFiles(IncludeDir);
                foreach (var f in files)
                {
                    var hr = ht.DigestSourceFile(f);
                    Assert.IsNotNull(hr.Hash);
                }
            }
        }

        [Test]
        [Repeat(2)]
        public void ThreadyHashIncludeFiles()
        {
            using (var ic = FileCacheStore.Load("testincs"))
            {
                var ht = new HashUtil(ic);

                var hashes = ht.ThreadyDigestFiles(Directory.GetFiles(IncludeDir), true);
                Assert.IsTrue(hashes.Count > 0);
            }
        }

        [Test]
        [Repeat(2)]
        public void ThreadyHashIncludeFilesCacheTest()
        {
            using (var ic = FileCacheStore.Load("testincs"))
            {
                var ht = new HashUtil(ic);

                var hashes = ht.DigestFiles(Directory.GetFiles(IncludeDir));
                Assert.IsTrue(hashes.Count > 0);
                System.Threading.Thread.Sleep(500);
                var hashes2 = ht.DigestFiles(Directory.GetFiles(IncludeDir));
                foreach (var h in hashes2)
                {
                    Assert.IsTrue(h.Value.Cached);
                }
            }
        }

        [Test]
        [Repeat(2)]
        public void HashesMatch()
        {
            var files = Directory.GetFiles(IncludeDir);
            using (var ic = FileCacheStore.Load("testincs"))
            {
                var ht = new HashUtil(ic);
                var hashes = ht.ThreadyDigestFiles(files, true);
                foreach (var f in files)
                {
                    var hash = ht.DigestSourceFile(f);

                    if (hash.Result == DataHashResult.Ok)
                    {
                        Assert.AreEqual(hash.Hash, hashes[f.ToLower()].Hash);
                    }
                }

                Assert.AreEqual(files.Length, hashes.Count);
            }
        }
    }
}
