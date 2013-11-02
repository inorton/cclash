using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace CClash
{
    public class DirectoryWatcher : IDisposable
    {
        FileSystemWatcher w;

        public delegate void FileChangedHandler(string filepath);

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

        List<string> Files { get; set; }

        void OnChange(string file)
        {
            file = Path.GetFileName(file);
            if (Files.Contains(file))
            {
                if (FileChanged != null)
                {
                    FileChanged(file);
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
