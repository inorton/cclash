using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public class ParseTrackerFile
    {
        public static List<string> GetFiles(string folder)
        {
            var files = new List<string>();
            foreach (var fname in System.IO.Directory.EnumerateFiles(folder)) {
                files.Add(fname);
            }
            return files;
        }

        public static List<string> Parse(string filename)
        {
            var lines = System.IO.File.ReadAllLines(filename);
            var rv = new List<string>();
            foreach (var line in lines)
            {
                if (!line.StartsWith("#"))
                {
                    var fname = line.Trim();
                    rv.Add(fname);
                }
            }
            return rv;
        }

        public static List<string> ParseReads(string folder)
        {
            var found = new List<string>();
            foreach (var fname in GetFiles(folder))
            {
                if (fname.Contains(".read."))
                {
                    found.AddRange(Parse(Path.Combine(folder, fname)));
                }
            }
            return found;
        }
    }
}
