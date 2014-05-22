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
        GetResult = 3,
    }

    [Serializable]
    public class CClashMessage
    {
        public byte[] Serialize()
        {
            using (var ms = new MemoryStream())
            {
                Serialize(ms);
                return ms.ToArray();
            }
        }

        public void Serialize( Stream str )
        {
            new BinaryFormatter().Serialize(str, this);
        }

        public static T Deserialize<T>(Stream str)
        {
            return (T)(new BinaryFormatter().Deserialize(str));
        }

        public static T Deserialize<T>(byte[] bb)
            where T : CClashMessage, new()
        {
            var rv = new T();
            using (var ms = new MemoryStream(bb))
            {
                rv = Deserialize<T>(ms);
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
        public int tag;
    }

    [Serializable]
    public class CClashResponse : CClashMessage
    {
        public bool supported;
        public int exitcode;
        public string stderr;
        public string stdout;
        public int tag;
    }


}
