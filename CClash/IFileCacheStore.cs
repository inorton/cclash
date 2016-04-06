using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public delegate void FileCacheStoreAddedHandler(object sender, FileCacheStoreAddedEventArgs e);
    public delegate void FileCacheStoreRemovedHandler(object sender, FileCacheStoreRemovedEventArgs e);


    public interface IFileCacheStore : IDisposable
    {
        void Open(string folderPath);

        void WaitOne();
        void ReleaseMutex();

        event FileCacheStoreAddedHandler Added;
        event FileCacheStoreRemovedHandler Removed;

        bool CacheEntryChecksInMemory { get; set; }
        string FolderPath { get; }

        void ClearLocked();

        void EnsureKey(string key);
        Stream OpenFileStream(string key, string filename, FileMode mode, FileAccess access);

        bool ContainsEntry(string key, string filename);
        void Remove(string key);
        void AddEntry(string key);
        void AddFile(string key, string filePath, string contentName);
        void AddTextFileContent(string key, string filename, string content);
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
