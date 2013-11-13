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
            StdErrorText = new StringBuilder();
            StdOutText = new StringBuilder();
            base.includeCache.KeepLocks();
            base.outputCache.KeepLocks();
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

        public void OnWatchedFileChanged(string path)
        {
            lock (hashcache)
            {
                hashcache.Remove(path);
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
                        hashcache[filename.ToLower()] =  tmp[filename];
                        rv[filename.ToLower()] = tmp[filename];
                        WatchFile(filename.ToLower());
                    }
                }
            }

            return rv;  
        }

        public StringBuilder StdErrorText { get; private set; }
        public StringBuilder StdOutText { get; private set; }

        public override void OutputWriteLine(string str)
        {
            StdOutText.AppendLine(str);
        }

        public override void ErrorWriteLine(string str)
        {
            StdErrorText.AppendLine(str);
        }

        public override void OutputWrite(string str)
        {
            StdOutText.Append(str);
        }

        public override void ErrorWrite(string str)
        {
            StdErrorText.Append(str);
        }

        public int CompileOrCacheEnvs( string workdir, IDictionary<string,string> envs, IEnumerable<string> args)
        {
            StdErrorText.Clear();
            StdOutText.Clear();
            // not entirely sure why i need to do both of these..
            Directory.SetCurrentDirectory(workdir);
            foreach (var e in envs)
            {
                Environment.SetEnvironmentVariable(e.Key, e.Value);
            }
            return base.CompileOrCache(args);
        }

        public override void Dispose()
        {
            foreach (var x in dwatchers)
            {
                x.Value.Dispose();
            }
            dwatchers.Clear();
            base.includeCache.UnKeepLocks();
            base.outputCache.UnKeepLocks();
            base.Dispose();
        }

        void SetupStats()
        {
            if (Stats != null)
                Stats.Dispose();
            Stats = new FastCacheInfo(outputCache);
        }

        int yield_count = 0;
        public void YieldLocks()
        {
            if (yield_count++ > 100)
            {
                yield_count = 0;
                SetupStats();
            }

            base.includeCache.UnKeepLocks();
            base.outputCache.UnKeepLocks();

            System.Threading.Thread.Sleep(0);

            base.includeCache.KeepLocks();
            base.outputCache.KeepLocks();
        }
    }
}
