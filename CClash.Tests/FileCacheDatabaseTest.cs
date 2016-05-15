using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using CClash;

namespace CClash.Tests
{
    [TestFixture]
    public class FileCacheDatabaseTest : FileCacheTestBase
    {

        [Test]
        public void TestPopulateCacheSQLite()
        {
            PopulateTest<FileCacheDatabase>();
        }

        [Test]
        public void TestReadCacheSQLite()
        {
            ReadTest<FileCacheDatabase>();
        }
    }
}
