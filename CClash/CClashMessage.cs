using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace CClash
{
    [Serializable]
    public enum Command
    {
        Run = 0,
        GetStats = 1,
        Quit = 2,
    }

    [Serializable]
    public class CClashMessage
    {
        public byte[] Serialize()
        {
            var b = new BinaryFormatter();
            using (var ms = new MemoryStream())
            {
                b.Serialize(ms, this);
                return ms.ToArray();
            }
        }

        public static T Deserialize<T>(byte[] bb)
            where T : CClashMessage, new()
        {
            var rv = new T();
            var b = new BinaryFormatter();
            using (var ms = new MemoryStream(bb))
            {
                rv = (T)b.Deserialize(ms);
            }
            return rv;
        }
    }

    [Serializable]
    public class CClashRequest : CClashMessage
    {
        public Command cmd;
        public string workdir;
        public IDictionary<string, string> envs;
        public IList<string> argv;
        public string compiler;
    }

    [Serializable]
    public class CClashResponse : CClashMessage
    {
        public bool supported;
        public int exitcode;
        public string stderr;
        public string stdout;
    }


}
