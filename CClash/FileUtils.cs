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

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
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

    }
}
