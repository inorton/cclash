using System;
using System.Collections.Generic;
using System.IO;

namespace CClash
{
    public class FileChangedEventArgs : EventArgs
    {
        public string FilePath { get; set; }
    }

    public sealed class DirectoryWatcher : IDisposable
    {
        FileSystemWatcher w;

        public delegate void FileChangedHandler(object sender, FileChangedEventArgs e);

        public DirectoryWatcher(string folder)
        {
            Files = new List<string>();
            w = new FileSystemWatcher(folder);
            w.Changed += (o, a) => { OnChange(a.FullPath); };
            w.Deleted += (o, a) => { OnChange(a.FullPath); };
            w.Renamed += (o, a) => { OnChange(a.FullPath); };
        }

        public event FileChangedHandler FileChanged;

        public void Enable()
        {
            w.EnableRaisingEvents = true;
        }

        public void Watch(string file)
        {

            lock (Files)
            {
                if (!Files.Contains(file))
                {
                    Files.Add(file);
                }
            }
        }

        public void UnWatch(string file)
        {
            lock (Files)
            {
                if (Files.Contains(file))
                    Files.Remove(file);
            }
        }

        public List<string> Files { get; private set; }

        void OnChange(string file)
        {
            file = Path.GetFileName(file);
            if (Files.Contains(file))
            {
                if (FileChanged != null)
                {
                    FileChanged(this, new FileChangedEventArgs() { FilePath = file });
                }
            }
        }

        public void Dispose()
        {
            if (w.EnableRaisingEvents)
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
                w = null;
            }
        }
    }
}
