using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;


namespace CClash
{
    public enum DataHashResult
    {
        Ok,
        ContainsTimeOrDate,
        FileNotFound,
        AccessDenied,
        FileAdded,
        FileChanged,
        NoPreviousBuild,
        CacheCorrupt,        
    }

    public sealed class DataHash
    {
        public string InputName { get; set; }
        public DataHashResult Result { get; set; }
        
        public string Hash { get; set; }

        // hash of everything (compiler, cwd, envs, args) except the source file content
        public string SessionHash { get; set; }
        // hash of the source file
        public string SourceHash { get; set; }
        public DateTime TimeStamp { get; set; }
        public bool Cached { get; set; }

        public DataHash()
        {
            TimeStamp = DateTime.Now;
        }

        public TimeSpan Age
        {
            get
            {
                return DateTime.Now.Subtract(TimeStamp);
            }
        }
    }

    public sealed class HashUtil
    {
        const string FindDateTimePattern = "__(TIM|DAT)E__";
        const string F_HasDateTime = "hasdatetime";
        const string F_NotDateTime = "notdatetime";

        const int SavedHashMaxAgeMinutes = 3;

        static Regex FindDateTime = new Regex(FindDateTimePattern);

        IFileCacheStore includeCache;

        public HashUtil(IFileCacheStore includecache) {
            if (includecache == null) throw new ArgumentNullException("includecache");
            includeCache = includecache;
        }

        private int hashingThreadCount = Settings.HashThreadCount;

        public int HashingThreadCount
        {
            get { return hashingThreadCount; }
            set { hashingThreadCount = value; }
        }

        Dictionary<string, DataHash> recentHashes = new Dictionary<string, DataHash>();
        ReaderWriterLockSlim recentHashLock = new ReaderWriterLockSlim();

        public DataHash DigestString(string input)
        {
            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();

            var rv = new DataHash()
            {
                InputName = "string",
                Result = DataHashResult.Ok,
                Hash = new SoapHexBinary( md5.ComputeHash( System.Text.Encoding.Unicode.GetBytes( input ) ) ).ToString()
            };
            return rv;
        }

        public DataHash DigestStream(Stream s)
        {
            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
            var rv = new DataHash()
            {
                InputName = "stream",
                Result = DataHashResult.Ok,
                Hash = new SoapHexBinary( md5.ComputeHash(s) ).ToString()
            };
            return rv;
        }

        public Dictionary<string,DataHash> DigestFiles(IEnumerable<string> files, string workdir)
        {
            var tohash = new List<string>();
            Dictionary<string, DataHash> rv = new Dictionary<string, DataHash>();

            recentHashLock.EnterReadLock();

            foreach (var f in files.Distinct())
            {
                var filepath = f.ToLower();
                if (!Path.IsPathRooted(filepath))
                {
                    if (!string.IsNullOrEmpty(workdir))
                    {
                        filepath = Path.Combine(workdir, filepath);
                    }
                }

                if (recentHashes.ContainsKey(filepath) && (recentHashes[filepath].Age.TotalMinutes < SavedHashMaxAgeMinutes))
                {
                    rv[filepath] = recentHashes[filepath];
                    rv[filepath].Cached = true;
                }
                else
                {
                    tohash.Add(filepath);
                }
            }

            recentHashLock.ExitReadLock();
            var newhashes = ThreadyDigestFiles(tohash, true);
            recentHashLock.EnterWriteLock();
            foreach (var nh in newhashes)
            {
                rv[nh.Key] = nh.Value;
                recentHashes[nh.Key] = nh.Value;
            }
            recentHashLock.ExitWriteLock();
            return rv;
        }

        public Dictionary<string, DataHash> ThreadyDigestFiles(IEnumerable<string> files, bool stopOnUnCachable)
        {
            lock (includeCache)
            {
                var fcount = files.Count();
                var rv = new Dictionary<string, DataHash>();
                var threadcount = HashingThreadCount;
                if ((threadcount < 2) || (fcount < threadcount))
                {
                    Logging.Emit("st hash {0} files", fcount);
                    foreach (var f in files)
                    {
                        var d = DigestSourceFile(f);
                        rv[f.ToLower()] = d;
                        if (d.Result != DataHashResult.Ok) break;
                    }
                }
                else
                {
                    Logging.Emit("mt hash {0} files on {1} threads", fcount, threadcount);
                    var fa = files.ToArray();
                    var tl = new List<Thread>();
                    var taken = 0;
                    var chunk = (1 + fcount / (threadcount));
                    if (chunk < 1) chunk = 1;

                    var inputs = new List<ThreadyDigestInput>();

                    do
                    {
                        var input = new ThreadyDigestInput()
                        {
                            files = fa,
                            results = new List<DataHash>(),
                            provider = new MD5CryptoServiceProvider(),
                            begin = taken,
                            chunksize = chunk,
                            stopOnCachable = stopOnUnCachable,
                        };

                        var t = new Thread(ThreadyDigestWorker);
                        taken += chunk;
                        t.Start(input);
                        inputs.Add(input);
                        tl.Add(t);
                    } while (taken < fcount);

                    for (var i = 0; i < tl.Count; i++)
                    {
                        var t = tl[i];
                        t.Join(); // thread finished, store it's results
                        foreach (var h in inputs[i].results)
                        {
                            rv[h.InputName.ToLower()] = h;
                        }
                    }
                }

                return rv;
            }
        }

        struct ThreadyDigestInput
        {
            public int begin;
            public int chunksize;
            public string[] files;
            public List<DataHash> results;
            public MD5 provider;
            public bool stopOnCachable;
        }

        void ThreadyDigestWorker(object arg)
        {
            var input = (ThreadyDigestInput)arg;
            var files = input.files;
            var end = input.begin + input.chunksize;
            if (end > files.Length) end = files.Length;
            var rx = new Regex(FindDateTimePattern);
            var hashed = new List<DataHash>();
            for ( var i = input.begin; i < end; i++ )
            {
                var d = DigestFile( input.provider, files[i], rx );
                hashed.Add(d);
                input.results.Add(d);
                if (input.stopOnCachable && d.Result != DataHashResult.Ok) break;
            }

        }

        public DataHash DigestSourceFile(string filepath)
        {
            return DigestFile(filepath, true);
        }

        public DataHash DigestBinaryFile(string filepath) 
        {
            try {
                recentHashLock.EnterReadLock();   
                if (recentHashes.ContainsKey(filepath) && recentHashes[filepath].Age.TotalMinutes < SavedHashMaxAgeMinutes)
                {
                    return recentHashes[filepath];
                }
            } finally {
                recentHashLock.ExitReadLock();
            }

            recentHashLock.EnterWriteLock();
            var rv = DigestFile(filepath, false);
            recentHashes[filepath] = rv;
            recentHashLock.ExitWriteLock();
            return rv;
        }

        DataHash DigestFile(string filepath, bool checkDateTime) 
        {
            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
            return DigestFile(md5, filepath, checkDateTime ? FindDateTime : null);
        }

        DataHash DigestFile( MD5 provider, string filepath, Regex findDateTime)
        {
            var rv = new DataHash() {
                Result = DataHashResult.FileNotFound,
                InputName = filepath,
            };

            if (!FileUtils.Exists(filepath)) return rv;
            provider.Initialize();
            var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read, 2048, FileOptions.SequentialScan);
            using (var bs = new BufferedStream(fs))
            {
                Logging.Emit("digest {0}", filepath);
                rv.Hash = new SoapHexBinary(provider.ComputeHash(bs)).ToString();
                rv.Result = DataHashResult.Ok;

                
                if (findDateTime != null)
                {
                    bool mark_with_datetime = false;
                    if (Settings.HonorCPPTimes)
                    {
                        // check include cache for this file                    
                        if (includeCache.ContainsEntry(rv.Hash, F_NotDateTime))
                        {
                            return rv;
                        }
                        if (includeCache.ContainsEntry(rv.Hash, F_HasDateTime))
                        {
                            rv.Result = DataHashResult.ContainsTimeOrDate;
                            return rv;
                        }

                        
                        bs.Seek(0, SeekOrigin.Begin);
                        using (var ts = new StreamReader(bs))
                        {
                            string line = null;
                            do
                            {
                                line = ts.ReadLine();
                                if (line != null)
                                {
                                    if (findDateTime.IsMatch(line))
                                    {
                                        mark_with_datetime = true;
                                        break;
                                    }
                                }

                            } while (line != null);
                        }
                    }

                    includeCache.WaitOne();
                    if (!mark_with_datetime)
                    {
                        includeCache.AddTextFileContent(rv.Hash, F_NotDateTime, "");
                    }
                    else
                    {
                        includeCache.AddTextFileContent(rv.Hash, F_HasDateTime, "");
                    }
                    includeCache.ReleaseMutex();
                    
                }
            }
            rv.Result = DataHashResult.Ok;
            
            return rv;
        }
    }
}
