using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CClash {
    public class FileUtils 
    {

        public static bool Exists(string path)
        {
            return new FileInfo(path).Exists;
        }
    }
}
