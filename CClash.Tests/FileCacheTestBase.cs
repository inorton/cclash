using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using NUnit.Framework;

namespace CClash.Tests
{
    public class FileCacheTestBase
    {
        public string TempFolder { get; protected set; }

        [TestFixtureSetUp]
        public void MakeTempFolder()
        {
            var fname = Guid.NewGuid().ToString();
            var tmp = Path.GetTempPath();
            TempFolder = Path.Combine(tmp, fname);
            Directory.CreateDirectory(TempFolder);
        }

        [TestFixtureTearDown]
        public void CleanTempFolder()
        {
            if (!String.IsNullOrEmpty(TempFolder))
            {
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        if (Directory.Exists(TempFolder))
                            Directory.Delete(TempFolder, true);
                        return;
                    }
                    catch
                    {
                        System.Threading.Thread.Sleep(200);
                    }
                }
            }
        }

        public void PopulateTest<T>() where T : FileCacheBase, IFileCacheStore, new()
        {
            int nfiles = 100;
            int filesize = 1024 * 1024 * 2;
            var ms = new MemoryStream(filesize);

            using (var db = FileCacheStore.Load<T>(TempFolder))
            {
                for (int i = 0; i < nfiles; i++)
                {
                    string fname = "populate" + i.ToString();
                    using (var ws = db.OpenFileStream("cdef", fname, FileMode.CreateNew, FileAccess.Write))
                    {
                        ms.CopyTo(ws);
                    }
                }
            }
        }

        public void ReadTest<T>() where T : FileCacheBase, IFileCacheStore, new()
        {
            int nfiles = 100;
            int filesize = 1024 * 1024 * 2;
            var ms = new MemoryStream(filesize);

            using (var db = FileCacheStore.Load<T>(TempFolder))
            {
                for (int i = 0; i < nfiles; i++)
                {
                    string fname = "populate" + i.ToString();
                    using (var ws = db.OpenFileStream("cdef", fname, FileMode.Open, FileAccess.Read))
                    {
                        ws.CopyTo(ms);
                    }
                }
            }
        }

    }
}
