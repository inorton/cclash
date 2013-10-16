using System;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;

namespace CClash
{
    public class FileCacheStore : IDisposable
    {
        public delegate void FileCacheStoreAddedHandler(FileCacheStore cache, FileCacheStoreAddedEventArgs args);
        public delegate void FileCacheStoreRemovedHandler(FileCacheStore cache, FileCacheStoreRemovedEventArgs args);

        public static FileCacheStore Load(string cacheFolder)
        {
            return new FileCacheStore(cacheFolder);
        }

        public string FolderPath { get; private set; }
        JavaScriptSerializer jss = new JavaScriptSerializer();
        Mutex mtx = null;

        public void WaitOne()
        {
            if (!mtx.WaitOne()) throw new InvalidProgramException("mutex lock failed " + mtx.ToString() );
        }

        public void ReleaseMutex()
        {
            mtx.ReleaseMutex();
        }

        FileCacheStore( string folderPath )
        {
            FolderPath = Path.GetFullPath(folderPath);
            mtx = new Mutex(false, "cclash_mtx_" + FolderPath.ToLower().GetHashCode());
            WaitOne();
            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
            }
            // make the top level folders
            for (int i = 0; i < 256; i++)
            {
                var fp = Path.Combine(FolderPath, string.Format("{0:x2}", i));
                if (!Directory.Exists(fp)) Directory.CreateDirectory(fp);
            }
            ReleaseMutex();
            
        }

        public string MakePath(string key )
        {
            var tlf = key.Substring(0, 2);
            return Path.Combine(FolderPath, tlf, key);
        }

        public string MakePath(string key, string contentFile)
        {
            var tlf = key.Substring(0, 2);
            return Path.Combine(FolderPath, tlf, key, contentFile);
        }

        public event FileCacheStoreAddedHandler Added;

        void EnsureKey(string key)
        {
            var kp = MakePath(key);
            if (!Directory.Exists(kp))
            {
                Directory.CreateDirectory(kp);
            }
        }

        public void AddFile(string key, string filePath, string contentName)
        {
            EnsureKey(key);
            File.Copy(filePath, MakePath(key, contentName));
            if (Added != null)
            {
                Added(this, new FileCacheStoreAddedEventArgs() { SizeKB = (int)(new FileInfo(filePath).Length / 1024) });
            }
        }

        public void AddTextFileContent(string key, string filename, string content)
        {
            EnsureKey(key);
            File.WriteAllText(MakePath(key, filename), content);
            if (Added != null)
            {
                Added(this, new FileCacheStoreAddedEventArgs() { SizeKB = content.Length * sizeof(char) });
            }
        }

        public event FileCacheStoreRemovedHandler Removed;

        public void Remove(string key)
        {
            var p = MakePath(key);
            if (Directory.Exists(p))
            {
                int sz = 0;
                var di = new DirectoryInfo(p);
                foreach (var f in di.GetFiles())
                {
                    sz += (int)(f.Length / 1024);
                }
                Directory.Delete(MakePath(key), true);
                if (Removed != null)
                {
                    Removed(this, new FileCacheStoreRemovedEventArgs() { SizeKB = sz });
                }
            }
        }

        public void Dispose()
        {
            if (mtx != null) mtx.Dispose();
        }
    }

    public class FileCacheStoreAddedEventArgs : EventArgs
    {
        public int SizeKB { get; set; }
    }

    public class FileCacheStoreRemovedEventArgs : EventArgs
    {
        public int SizeKB { get; set; }
    }
}
