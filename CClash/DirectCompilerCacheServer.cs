using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;

namespace CClash
{

    public sealed class DirectCompilerCacheServer : DirectCompilerCache
    {
        Dictionary<string, DirectoryWatcher> dwatchers = new Dictionary<string, DirectoryWatcher>();

        public DirectCompilerCacheServer(string cachedir)
            : base(cachedir)
        {
            SetupStats();
            base.includeCache.CacheEntryChecksInMemory = true;
        }

        public override void Setup()
        {
        }

        public override void Finished()
        {
           
        }

        public void WatchFile(string path)
        {
            if (!path.ToLower().Contains(":\\progra")) return;
            var dir = Path.GetDirectoryName(path);
            if ( !Path.IsPathRooted(dir) )
                dir = Path.GetFullPath(dir);

            if (!Directory.Exists(dir))
            {
                Logging.Error("ignored watch on missing folder {0}", dir);
                return;
            }

            DirectoryWatcher w;

            if (!dwatchers.TryGetValue(dir, out w))
            {
                if (!FileExists(path))
                {
                    Logging.Error("ignored watch on missing file {0}", path);
                    return;
                }

                Logging.Emit("create new watcher for {0}", dir);
                w = new DirectoryWatcher(dir);
                dwatchers.Add(dir, w);
                w.FileChanged += OnWatchedFileChanged;
                w.Enable();
            }
            var file = Path.GetFileName(path);
            w.Watch(file);
        }

        public void UnwatchFile(string path)
        {
            var dir = Path.GetDirectoryName(path);
            DirectoryWatcher w;

            if (dwatchers.TryGetValue(dir, out w))
            {
                var file = Path.GetFileName(path);
                w.UnWatch(file);
                if (w.Files.Count == 0)
                {
                    dwatchers.Remove(dir);
                    w.Dispose();
                }
            }
        }

        public void OnWatchedFileChanged( object sender, FileChangedEventArgs args)
        {
            lock (hashcache)
            {
                hashcache.Remove(args.FilePath);
            }
        }

        public override bool FileExists(string path)
        {
            lock (hashcache)
            {
                if (hashcache.ContainsKey(path))
                    return true;
            }
            return base.FileExists(path);
        }

        public override DataHash DigestCompiler(string compilerPath)
        {
            lock (hashcache)
            {
                if (hashcache.ContainsKey(compilerPath))
                    return hashcache[compilerPath];

                var h = base.DigestCompiler(compilerPath);

                WatchFile(compilerPath);

                hashcache.Add(compilerPath, h);

                return h;
            }
        }

        Dictionary<string, DataHash> hashcache = new Dictionary<string, DataHash>();

        public override Dictionary<string, DataHash> GetHashes(IEnumerable<string> fnames)
        {
            if (hashcache.Count > 20000)
            {
                lock (hashcache)
                {
                    hashcache.Clear();
                }
            }

            var unknown = new List<string>();
            var rv = new Dictionary<string, DataHash>();
            foreach (var n in fnames)
            {
                var x = n.ToLower();
                lock (hashcache)
                {
                    if (hashcache.ContainsKey(x))
                    {
                        rv[x] = hashcache[x];
                    }
                    else
                    {
                        unknown.Add(x);
                    }
                }
            }

            if (unknown.Count > 0)
            {
                Logging.Emit("hash {0}/{1} new/changed files", unknown.Count, fnames.Count());
                var tmp = base.GetHashes(fnames);
                lock (hashcache)
                {
                    foreach (var filename in tmp.Keys)
                    {
                        var flow = filename.ToLower();
                        hashcache[flow] = tmp[filename];
                        rv[flow] = tmp[filename];
                        WatchFile(flow);
                    }
                }
            }

            return rv;  
        }

        public override int CompileOrCache(ICompiler comp, IEnumerable<string> args)
        {
            return base.CompileOrCache(comp, args);
        }

        protected override void Dispose( bool disposing)
        {
            if (disposing)
            {
                foreach (var x in dwatchers)
                {
                    x.Value.Dispose();
                }
                dwatchers.Clear();
            }
            base.Dispose(disposing);
        }

        public void SetupStats()
        {
            if (Stats != null)
                Stats.Dispose();
            Stats = new FastCacheInfo(outputCache);
        }
    }
}
