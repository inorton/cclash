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
    }

    public sealed class DataHash
    {
        public string InputName { get; set; }
        public DataHashResult Result { get; set; }
        public string Hash { get; set; }
    }

    public sealed class HashUtil
    {
        const string FindDateTimePattern = "__(TIM|DAT)E__";
        const string F_HasDateTime = "hasdatetime";
        const string F_NotDateTime = "notdatetime";

        static Regex FindDateTime = new Regex(FindDateTimePattern);

        FileCacheStore includeCache;

        public HashUtil(FileCacheStore includecache) {
            if (includecache == null) throw new ArgumentNullException("includecache");
            includeCache = includecache;
        }

        private int hashingThreadCount = Settings.HashThreadCount;

        public int HashingThreadCount
        {
            get { return hashingThreadCount; }
            set { hashingThreadCount = value; }
        }

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

        public Dictionary<string,DataHash> DigestFiles(IEnumerable<string> files)
        {
            return ThreadyDigestFiles(files, true);
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
                        rv[f] = d;
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
                            rv[h.InputName] = h;
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
            for ( var i = input.begin; i < end; i++ )
            {
                var d = DigestFile( input.provider, files[i], rx );
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
            return DigestFile(filepath, false);
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
            using (var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var bs = new BufferedStream(fs))
            {
                Logging.Emit("digest {0}", filepath);
                rv.Hash = new SoapHexBinary(provider.ComputeHash(bs)).ToString();
                rv.Result = DataHashResult.Ok;
                
                if (findDateTime != null) {
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
                            if (line == null) 
                            {
                                includeCache.WaitOne();
                                try
                                {
                                    includeCache.AddTextFileContent(rv.Hash, F_NotDateTime, "");
                                }
                                finally
                                {
                                    includeCache.ReleaseMutex();
                                }
                                break;
                            }

                            if (findDateTime.IsMatch(line))
                            {
                                rv.Result = DataHashResult.ContainsTimeOrDate;

                                includeCache.WaitOne();
                                try
                                {
                                    includeCache.AddTextFileContent(rv.Hash, F_HasDateTime, "");
                                }
                                finally
                                {
                                    includeCache.ReleaseMutex();
                                }
                                
                                break;
                            }

                        } while (true);
                    }
                    

                }
            }
            rv.Result = DataHashResult.Ok;
            
            return rv;
        }
    }
}
