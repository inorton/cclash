using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using CClash;

namespace CClash.Tests
{
    [TestFixture]
    public class FileCacheTest
    {
        string thistestdll = typeof(FileCacheTest).Assembly.Location;

        [Test]
        public void Test0DeleteCache()
        {
            if (Directory.Exists("clcachetest_init")) 
                Directory.Delete("clcachetest_init", true);
        }


        [Test]
        [TestCase("clcachetest_init")]
        public void Test1CacheSetup(string folder)
        {
            using (var fc = FileCacheStore.Load(folder))
            {
                Assert.IsTrue(Directory.Exists(fc.FolderPath));
            }
        }

        [Test]
        public void TestTextFileAddRemove()
        {
            using (var fc = FileCacheStore.Load("clcachetest_text"))
            {
                fc.WaitOne();
                try
                {
                    fc.AddTextFileContent("aa12345", "test.txt", "hello");
                    Assert.IsTrue(File.Exists(fc.MakePath("aa12345", "test.txt")));
                    fc.Remove("aa12345");
                    Assert.IsFalse(File.Exists(fc.MakePath("aa12345", "test.txt")));
                    Assert.IsTrue(Directory.Exists(Path.Combine(fc.FolderPath, "aa")));
                }
                finally
                {
                    fc.ReleaseMutex();
                }
            }
        }
    }
}
