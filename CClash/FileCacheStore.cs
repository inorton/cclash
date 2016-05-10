using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace CClash
{
    public class FileCacheStore : FileCacheBase, IFileCacheStore
    {
        public static IFileCacheStore Load<T>(string cachefolder) where T :  class, IFileCacheStore, new()
        {
            var cache = new T();
            cache.Open(cachefolder);
            return cache;
        }

        public static IFileCacheStore Load(string cacheFolder)
        {
            var ctype = Settings.CacheType;
            switch (ctype)
            {
                case CacheStoreType.FileCache:
                    return Load<FileCacheStore>(cacheFolder);
                case CacheStoreType.SQLite:
                    return Load<FileCacheDatabase>(cacheFolder);
                default:
                    throw new NotImplementedException(ctype.ToString());
            }      
        }

        public void Open( string folderPath )
        {
            FolderPath = Path.GetFullPath(folderPath);
            SetupLocks();
            Logging.Emit("locking file store: {0}", FolderPath);
            WaitOne();
            
            var tlist = new List<Thread>();
            try
            {
                if (Directory.Exists(FolderPath))
                {
                    bool bad_cache_format = false;
                    if (File.Exists(Path.Combine(FolderPath, CacheInfo.F_CacheVersion)))
                    {
                        var cdv = File.ReadAllText(Path.Combine(FolderPath, CacheInfo.F_CacheVersion));
                        bad_cache_format = cdv != CacheInfo.CacheFormat;
                    }

                    if (File.Exists(Path.Combine(FolderPath, CacheInfo.F_CacheType)))
                    {
                        var ct = File.ReadAllText(Path.Combine(FolderPath, CacheInfo.F_CacheType));
                        bad_cache_format = ct != Settings.CacheType.ToString();
                    }

                    if (bad_cache_format)
                    {
                        Logging.Emit("corrupt/new filestore, deleting: {0}", FolderPath);                        
                        Directory.Delete(FolderPath, true);
                    }
                }

                if (!Directory.Exists(FolderPath)){
                    Logging.Emit("create fresh filestore");
                    Directory.CreateDirectory(FolderPath);
                    File.WriteAllText(Path.Combine(FolderPath, CacheInfo.F_CacheVersion), CacheInfo.CacheFormat);
                    File.WriteAllText(Path.Combine(FolderPath, CacheInfo.F_CacheType), Settings.CacheType.ToString());
                }
                Logging.Emit("filestore ready: {0}", FolderPath);
            }
            catch (IOException)
            {
                throw new CClashErrorException("could not clear cache!");
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

        string MakePath(string key )
        {
            var tlf = key.Substring(0, 2);
            return Path.Combine(FolderPath, tlf, key);
        }

        string MakePath(string key, string contentFile)
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

        public void ClearLocked() {
            entryCache.Clear();
            var files = Directory.GetFiles(FolderPath);
            foreach (var f in files)
                File.Delete(f);
            var contents = Directory.GetDirectories(FolderPath);
            foreach (var d in contents)
                Directory.Delete(d, true);
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

        public Stream OpenFileStream(string key, string filename, FileMode mode, FileAccess access)
        {
            if (access == FileAccess.ReadWrite) throw new InvalidOperationException();
            return File.Open(MakePath(key, filename), mode, access);
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
    }
}
