using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace CClash
{
    public sealed class FileCacheStore : IDisposable
    {
        public delegate void FileCacheStoreAddedHandler(object sender, FileCacheStoreAddedEventArgs e);
        public delegate void FileCacheStoreRemovedHandler(object sender, FileCacheStoreRemovedEventArgs e);

        public static FileCacheStore Load(string cacheFolder)
        {
            return new FileCacheStore(cacheFolder);
        }

        public string FolderPath { get; private set; }
        
        Mutex mtx = null;
        bool ignoreLocks = false;
        public void KeepLocks()
        {
            WaitOne();
            ignoreLocks = true;
        }

        public void UnKeepLocks()
        {
            ignoreLocks = false;
            ReleaseMutex();
        }

        public void WaitOne()
        {
            if (!ignoreLocks)
            {
                Logging.Emit("WaitOne {0}", FolderPath);
                if (!mtx.WaitOne()) throw new InvalidProgramException("mutex lock failed " + mtx.ToString());
            }
        }

        public void ReleaseMutex()
        {
            if (!ignoreLocks)
            {
                Logging.Emit("ReleaseMutex {0}", FolderPath);
                mtx.ReleaseMutex();
            }
        }

        FileCacheStore( string folderPath )
        {
            FolderPath = Path.GetFullPath(folderPath);
            mtx = new Mutex(false, "cclash_mtx_" + FolderPath.ToLower().GetHashCode());

            WaitOne();
            
            var tlist = new List<Thread>();
            try
            {
                if (!Directory.Exists(FolderPath))
                {
                    Directory.CreateDirectory(FolderPath);
                }
                else
                {
                    bool bad_cache_format = true;
                    if (File.Exists(Path.Combine(FolderPath, CacheInfo.F_CacheVersion)))
                    {
                        var cdv = File.ReadAllText(Path.Combine(FolderPath, CacheInfo.F_CacheVersion));
                        bad_cache_format = cdv != CacheInfo.CacheFormat;
                    }

                    if (bad_cache_format)
                    {
                        // cache is too old, wiping
                        Directory.Delete(FolderPath, true);
                        Directory.CreateDirectory(FolderPath);
                        File.WriteAllText(Path.Combine(FolderPath, CacheInfo.F_CacheVersion), CacheInfo.CacheFormat);
                    }
                }
            }
            catch (UnauthorizedAccessException uae)
            {
                throw new CClashWarningException("cache access error: " + uae.Message);
            }
            finally
            {
                ReleaseMutex();
            }
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

        public void EnsureKey(string key)
        {
            var kp = MakePath(key);
            if (!Directory.Exists(kp.Substring(0,2))) 
            {
                Directory.CreateDirectory(kp.Substring(0,2));
            }
            if (!Directory.Exists(kp))
            {
                Directory.CreateDirectory(kp);
            }
        }

        public bool CacheEntryChecksInMemory
        {
            get;
            set;
        }

        HashSet<string> entryCache = new HashSet<string>();

        public bool ContainsEntry(string key, string filename)
        {
            var p = MakePath(key, filename);
            if (CacheEntryChecksInMemory)
            {
                if (entryCache.Contains(p))
                {
                    return true;
                }
            }
            var rv = FileUtils.Exists(p);
            if (CacheEntryChecksInMemory && rv)
            {
                entryCache.Add(p);
            }
            return rv;
        }

        public void AddEntry(string key)
        {
            EnsureKey(key);
        }

        public void AddFile(string key, string filePath, string contentName)
        {
            EnsureKey(key);
            var target = MakePath(key, contentName);

            FileUtils.CopyUnlocked(filePath, target);

            if (Added != null)
            {
                Added(this, new FileCacheStoreAddedEventArgs() { SizeKB = (int)(new FileInfo(filePath).Length / 1024) });
            }
        }

        public void AddTextFileContent(string key, string filename, string content)
        {
            EnsureKey(key);
            FileUtils.WriteTextFile(MakePath(key, filename), content);
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
            if (mtx != null)
            {
                if (ignoreLocks)
                    UnKeepLocks();
                mtx.Dispose();
            }
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
