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
        public abstract byte[] Serialize();

        public void Serialize( Stream str )
        {
            var bb = Serialize();
            str.Write(bb, 0, bb.Length);
        }

        public abstract void Deserialize(byte[] bb);

    }

    public class CClashRequest : CClashMessage
    {
        public Command cmd;
        public string workdir;
        public IDictionary<string, string> envs;
        public IList<string> argv;
        public string compiler;

        public override byte[] Serialize()
        {
            var sb = new StringBuilder();
            sb.AppendFormat(":cmd:{0}\n", cmd );
            sb.AppendFormat(":workdir:{0}\n", CClashMessageUtils.Escape(workdir));
            sb.AppendFormat(":compiler:{0}\n", CClashMessageUtils.Escape(compiler));
            if (argv != null)
            {
                sb.AppendFormat(":argv:{0}\n", CClashMessageUtils.Escape(string.Join("\t", argv.ToArray())));
            }
            if (envs != null)
            {
                foreach (var e in envs)
                {
                    sb.AppendFormat(":env:{0}={1}\n", CClashMessageUtils.Escape(e.Key), CClashMessageUtils.Escape(e.Value));
                }
            }

            return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        }

        public override void Deserialize(byte[] bb)
        {
            var newenv = new Dictionary<string, string>();
            var str = System.Text.Encoding.UTF8.GetString(bb);
            foreach (var line in str.Split('\n'))
            {
                if (line.Length > 1)
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
                                var pair = value.Split(new char[] { '=' }, 2);
                                if (pair.Length != 2)
                                {
                                    throw new InvalidDataException(string.Format("bad environment variable in message '{0}'", value));
                                }
                                newenv[pair[0]] = pair[1];
                                break;

                            default:
                                throw new InvalidDataException( string.Format("unknown field in message'{0}'", field));
                                
                        }
                    }
                }
            }
            envs = newenv;
        }
    }

    public class CClashResponse : CClashMessage
    {
        public bool supported;
        public int exitcode;
        public string stderr;
        public string stdout;

        public override byte[] Serialize()
        {
            if (stderr == null) stderr = string.Empty;
            if (stdout == null) stdout = string.Empty;

            var sb = new StringBuilder();
            sb.AppendFormat(":supported:{0}\n", supported);
            sb.AppendFormat(":exitcode:{0}\n", exitcode);

            if (stderr != null)
            {
                sb.AppendFormat(":stderr:{0}\n", CClashMessageUtils.Escape(stderr));
            }

            if (stdout != null)
            {
                sb.AppendFormat(":stdout:{0}\n", CClashMessageUtils.Escape(stdout));
            }

            return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        }

        public override void Deserialize(byte[] bb)
        {
            var newenv = new Dictionary<string, string>();
            var str = System.Text.Encoding.UTF8.GetString(bb);
            foreach (var line in str.Split('\n'))
            {
                if (line.Length > 1)
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
                            default:
                                throw new InvalidDataException(string.Format("unknown field in message'{0}'", field));

                        }
                    }
                }
            }
        }
    }


}
