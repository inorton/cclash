using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using CClash;

namespace CClast.Tests
{
    [TestFixture]
    public class FileCacheTest
    {
        string thistestdll = typeof(FileCacheTest).Assembly.Location;

        [Test]
        public void TestCacheSetup()
        {
            if ( Directory.Exists("clcachetest_init") ) Directory.Delete("clcachetest_init", true);
            using (var fc = FileCacheStore.Load("clcachetest_init"))
            {
                Assert.IsTrue(Directory.Exists(fc.FolderPath));
                var folders = Directory.GetDirectories(fc.FolderPath);
                Assert.IsTrue(folders.Length == 256);
                Directory.Delete(fc.FolderPath,true);
            }
        }

        [Test]
        public void TestTextFileAddRemove()
        {
            using (var fc = FileCacheStore.Load("clcachetest_text"))
            {
                fc.AddTextFileContent("aa12345", "test.txt", "hello");
                Assert.IsTrue(File.Exists(fc.MakePath("aa12345", "test.txt")));
                fc.Remove("aa12345");
                Assert.IsFalse(File.Exists(fc.MakePath("aa12345", "test.txt")));
                Assert.IsTrue(Directory.Exists(Path.Combine(fc.FolderPath, "aa")));
            }
        }
    }
}
