using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CClash {
    public sealed class FileUtils 
    {

        public static bool Exists(string path)
        {
            return new FileInfo(path).Exists;
        }
        
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, ThrowOnUnmappableChar = true, BestFitMapping = false)]
        static extern int GetLongPathName(
            [MarshalAs(UnmanagedType.LPTStr)] string path,
            [MarshalAs(UnmanagedType.LPTStr)] StringBuilder longPath,
            int longPathLength
            );

        public static string ToLongPathName(string path)
        {
            if ( !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path) && path.Contains("~"))
            {
                var sb = new StringBuilder(512);
                GetLongPathName(path, sb, sb.Capacity);
                return sb.ToString();
            }
            return path;
        }

        // this should be much faster if a file doesn't exist
        [DllImport("Shlwapi.dll", SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private extern static bool PathFileExists(StringBuilder path);

        static Dictionary<string, DateTime> recent_missing = new Dictionary<string, DateTime>();

        public static bool FileMissing(string path)
        {
            DateTime mt;
            DateTime now = DateTime.Now;
            if (recent_missing.TryGetValue(path, out mt))
            {
                if (now.Subtract(mt).TotalMilliseconds < 200) return true;
            }

            var sb = new StringBuilder(path);
            var missing = !PathFileExists(sb);
            if (missing)
            {
                lock (recent_missing)
                {
                    if (recent_missing.Count > 5000) recent_missing.Clear();
                    recent_missing[path] = DateTime.Now;
                }
            }
            return missing;
        }

        static int FileIORetrySleep = 100;
        static int FileIORetryCount = 4;

        public static void WriteTextFile(string path, string content)
        {
            int attempts = FileIORetryCount;
            do
            {
                try
                {
                    File.WriteAllText(path, content);
                    return;
                }
                catch (IOException)
                {
                    attempts--;
                    if (attempts == 0) throw;
                    System.Threading.Thread.Sleep(FileIORetrySleep);
                }
            } while (true);
        }

        public static void CopyUnlocked(string from, string to)
        {
            int attempts = FileIORetryCount;
            do
            {
                try
                {
                    using (var ifs = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        using (var ofs = new FileStream(to, FileMode.OpenOrCreate, FileAccess.Write))
                        {
                            ifs.CopyTo(ofs);
                            return;
                        }
                    }
                }
                catch (IOException)
                {
                    attempts--;
                    if (attempts == 0) throw;
                    System.Threading.Thread.Sleep(FileIORetrySleep);
                }
            } while (true);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static extern int GetShortPathName(
            [MarshalAs(UnmanagedType.LPTStr)]
         string path,
            [MarshalAs(UnmanagedType.LPTStr)]
         StringBuilder shortPath,
            int shortPathLength
            );

        public static string GetShortPath(string path)
        {
            var sb = new StringBuilder(255);
            GetShortPathName(path, sb, 255);
            return sb.ToString();
        }
    }
}
