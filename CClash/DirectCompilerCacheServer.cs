using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

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
            Stats = new FastCacheInfo(outputCache);
            base.includeCache.KeepLocks();
            base.outputCache.KeepLocks();
            base.includeCache.CacheEntryChecksInMemory = true;
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
            if (!FileExists(path))
            {
                Logging.Error("ignored watch on missing file {0}", path);
                return;
            }

            var file = Path.GetFileName(path);
            DirectoryWatcher w;

            if (!dwatchers.TryGetValue(dir, out w))
            {
                Logging.Emit("create new watcher for {0}", dir);
                w = new DirectoryWatcher(dir); 
                dwatchers.Add(dir, w);
                w.FileChanged += OnWatchedFileChanged;
                w.Enable();
            }
            w.Watch(file);
        }

        public void UnwatchFile(string path)
        {
            var dir = Path.GetDirectoryName(path);
            var file = Path.GetFileName(path);
            DirectoryWatcher w;

            if (dwatchers.TryGetValue(dir, out w))
            {
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
            foreach (var x in fnames)
            {
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
                        hashcache[filename] =  tmp[filename];
                        rv[filename] = tmp[filename];
                        WatchFile(filename);
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
            Environment.CurrentDirectory = workdir;
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
            Stats.Commit();
            base.Dispose();
        }

        public void YieldLocks()
        {
            base.includeCache.UnKeepLocks();
            base.outputCache.UnKeepLocks();

            System.Threading.Thread.Sleep(0);

            base.includeCache.KeepLocks();
            base.outputCache.KeepLocks();
        }
    }
}
