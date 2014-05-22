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
        None = 0,
        Run = 1,
        GetStats = 2,
        Quit = 3,
        NoOp = 4,
    }

    public static class CClashMessageUtils
    {
        public static string Escape(string s)
        {
            return System.Web.HttpUtility.UrlEncode(s, Encoding.UTF8);
        }

        public static string UnEscape(string s)
        {
            return System.Web.HttpUtility.UrlDecode(s, Encoding.UTF8);
        }
    }

    public abstract class CClashMessage
    {
        public abstract string Serialize();

        public byte[] ToBytes()
        {
            var str = Serialize();
            return System.Text.Encoding.UTF8.GetBytes(str);
        }

        public void SerializeMessage( Stream str )
        {
            var bb = ToBytes();
            str.Write(bb, 0, bb.Length);
        }

        public abstract void Deserialize(string s);

        public void Deserialize(byte[] bb)
        {
            var str = System.Text.Encoding.UTF8.GetString(bb);
            Deserialize(str);
        }
    }

    public class CClashRequest : CClashMessage
    {
        public Command cmd;
        public string workdir;
        public IDictionary<string, string> envs;
        public IList<string> argv;
        public string compiler;

        public override string Serialize()
        {
            var sb = new StringBuilder();
            sb.AppendLine(":begin:");
            sb.AppendLine(String.Format(":cmd:{0}", cmd ));
            sb.AppendLine(String.Format(":workdir:{0}", CClashMessageUtils.Escape(workdir)));
            sb.AppendLine(String.Format(":compiler:{0}", CClashMessageUtils.Escape(compiler)));
            if (argv != null)
            {
                sb.AppendLine(String.Format(":argv:{0}", CClashMessageUtils.Escape(string.Join("\t", argv.ToArray()))));
            }
            if (envs != null)
            {
                foreach (var e in envs)
                {
                    sb.AppendLine(String.Format(":env:{0}={1}", CClashMessageUtils.Escape(e.Key), CClashMessageUtils.Escape(e.Value)));
                }
            }
            sb.AppendLine(":end:");
            return sb.ToString();
        }

        public override void Deserialize(string str)
        {
            var newenv = new Dictionary<string, string>();
            using ( var sr = new StringReader(str) ) 
            {
                string line;
                do
                {
                    line = sr.ReadLine();
                    if (line != null && line.Length > 1)
                    {
                        var tmp = line.Split(new char[] { ':' }, 3);
                        if (tmp.Length > 2)
                        {
                            var field = tmp[1];
                            var value = CClashMessageUtils.UnEscape(tmp[2]);
                            switch (field)
                            {
                                case "cmd":
                                    cmd = (Command)Enum.Parse(typeof(Command), value);
                                    break;
                                case "workdir":
                                    workdir = value;
                                    break;
                                case "compiler":
                                    compiler = value;
                                    break;
                                case "argv":
                                    argv = new List<string>(value.Split('\t'));
                                    break;
                                case "env":
                                    var pair = tmp[2].Split(new char[] { '=' }, 2);
                                    if (pair.Length != 2)
                                    {
                                        throw new InvalidDataException(string.Format("bad environment variable in message '{0}'", value));
                                    }
                                    newenv[ CClashMessageUtils.UnEscape( pair[0]) ] = CClashMessageUtils.UnEscape( pair[1] );
                                    break;
                                case "begin":
                                    break;

                                case "end":
                                    envs = newenv;
                                    return;

                                default:
                                    throw new InvalidDataException(string.Format("unknown field in message'{0}'", field));

                            }
                        }
                    }
                } while (line != null);
            }
        }
    }

    public class CClashResponse : CClashMessage
    {
        public bool supported;
        public int exitcode;
        public string stderr;
        public string stdout;

        public override string Serialize()
        {
            if (stderr == null) stderr = string.Empty;
            if (stdout == null) stdout = string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine(":begin:");
            sb.AppendLine(String.Format(":supported:{0}", supported));
            sb.AppendLine(String.Format(":exitcode:{0}", exitcode));

            if (stderr != null)
            {
                sb.AppendLine(String.Format(":stderr:{0}", CClashMessageUtils.Escape(stderr)));
            }

            if (stdout != null)
            {
                sb.AppendLine(String.Format(":stdout:{0}", CClashMessageUtils.Escape(stdout)));
            }
            sb.AppendLine(":end:");
            return sb.ToString();
        }

        public override void Deserialize(string str)
        {
            var newenv = new Dictionary<string, string>();
            using ( var sr = new StringReader(str) ){
                string line = null;
                do
                {
                    line = sr.ReadLine();
                    if ( line != null && line.Length > 1)
                    {
                        var tmp = line.Split(new char[] { ':' }, 3);
                        if (tmp.Length > 2)
                        {
                            var field = tmp[1];
                            var value = CClashMessageUtils.UnEscape(tmp[2]);
                            switch (field)
                            {
                                case "supported":
                                    supported = Boolean.Parse(value);
                                    break;
                                case "exitcode":
                                    exitcode = Int32.Parse(value);
                                    break;
                                case "stderr":
                                    stderr = value;
                                    break;
                                case "stdout":
                                    stdout = value;
                                    break;
                                case "begin":
                                    break;
                                case "end":
                                    return;
                                default:
                                    throw new InvalidDataException(string.Format("unknown field in message'{0}'", field));

                            }
                        }
                    }
                } while (line != null);
            }
        }
    }
}
