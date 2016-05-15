using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using CClash;

namespace CClash.Tests
{
    [TestFixture]
    public class FileCacheTest : FileCacheTestBase
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
                    Assert.IsTrue(fc.ContainsEntry("aa12345", "test.txt"));                    
                    fc.Remove("aa12345");
                    Assert.IsFalse(fc.ContainsEntry("aa12345", "test.txt"));                    
                }
                finally
                {
                    fc.ReleaseMutex();
                }
            }
        }

        [Test]
        [Repeat(1000)]
        public void FileMissing_FileDotExists()
        {
            File.Exists("c:\\nosuchfile.txt");
        }

        [Test]
        [Repeat(1000)]
        public void FileMissing_FileUtilsExists()
        {
            FileUtils.Exists("c:\\nosuchfile.txt");
        }

        [Test]
        [Repeat(1000)]
        public void FileMissing_FileUtilsFileMissing()
        {
            FileUtils.FileMissing("c:\\nosuchfile.txt");
        }

        [Test]
        public void TestPopulateCacheFiles()
        {
            PopulateTest<FileCacheStore>();
        }

        [Test]
        public void TestReadCacheFiles()
        {
            ReadTest<FileCacheStore>();
        }
    }
}
