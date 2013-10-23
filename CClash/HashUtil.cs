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

    public class DataHash
    {
        public string InputName { get; set; }
        public DataHashResult Result { get; set; }
        public string Hash { get; set; }
    }

    public class HashUtil
    {
        const string FindDateTimePattern = "__(TIM|DAT)E__";
        static Regex FindDateTime = new Regex(FindDateTimePattern);
        static MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();

        private int hashingThreadCount = Environment.ProcessorCount;

        public int HashingThreadCount
        {
            get { return hashingThreadCount; }
            set { hashingThreadCount = value; }
        }

        public DataHash DigestString(string input)
        {
            var rv = new DataHash()
            {
                InputName = "string",
                Result = DataHashResult.Ok,
                Hash = new SoapHexBinary( md5.ComputeHash( System.Text.Encoding.Unicode.GetBytes( input ) ) ).ToString()
            };
            return rv;
        }

        public Dictionary<string,DataHash> DigestFiles(IEnumerable<string> files)
        {
            return ThreadyDigestFiles(files, true);
        }

        public Dictionary<string, DataHash> ThreadyDigestFiles(IEnumerable<string> files, bool stopOnUnCachable)
        {
            var fcount = files.Count();
            var rv = new Dictionary<string, DataHash>();
            var threadcount = HashingThreadCount;
            if ((threadcount < 2)||(fcount < threadcount))
            {
                foreach (var f in files)
                {
                    var d = DigestFile(f);
                    rv[f] = d;
                    if (d.Result != DataHashResult.Ok) break;
                }
            }
            else
            {
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

                for (var i = 0; i < tl.Count; i++ )
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

            for ( var i = input.begin; i < end; i++ )
            {
                var d = DigestFile( input.provider, files[i], new Regex(FindDateTimePattern) );
                input.results.Add(d);
                if (input.stopOnCachable && d.Result != DataHashResult.Ok) break;
            }
        }

        public DataHash DigestFile(string filepath)
        {
            return DigestFile(md5, filepath, FindDateTime);
        }

        DataHash DigestFile( MD5 provider, string filepath, Regex findDateTime)
        {
            var rv = new DataHash() {
                Result = DataHashResult.FileNotFound,
                InputName = filepath,
            };

            using (var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read))
            {
                var txt = File.ReadAllText(filepath);
                if (findDateTime.IsMatch(txt))
                {
                    rv.Result = DataHashResult.ContainsTimeOrDate;
                }
                else
                {
                    rv.Hash = new SoapHexBinary(provider.ComputeHash(System.Text.Encoding.Unicode.GetBytes(txt))).ToString();
                    rv.Result = DataHashResult.Ok;
                }

            }

            return rv;
        }
    }
}
