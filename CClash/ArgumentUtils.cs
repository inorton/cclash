using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CClash
{
    /// <summary>
    /// Class for doing things to command line arguments and argument lists
    /// </summary>
    public class ArgumentUtils
    {
        static char[] force_escape = new char[] {
            '\t', ' '
        };

        /// <summary>
        /// Join a list of command line args such that it can be passed as a single
        /// escaped string for subprocess arguments.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static string JoinAguments(IEnumerable<string> args)
        {

            var sb = new System.Text.StringBuilder(512);
            foreach (string arg in args)
            {
                string a = arg;
                if (a.Contains("\""))
                {
                    sb.Append(a.Replace("\"", "\\\""));
                }
                else
                {
                    if (a.IndexOfAny(force_escape) != -1)
                    {
                        sb.Append('\"');
                        sb.Append(a);
                        sb.Append('\"');
                    }
                    else
                    {
                        sb.Append(a);
                    }
                    sb.Append(" ");
                }
            }
            return sb.ToString().TrimEnd();
        }

        public static bool TargetIsFolder(string target)
        {
            switch (target.Last()) {
                case '/':
                case '\\':
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Given a whole path or single file, append .obj if the file has no dots in it's name.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public static string TargetObject(string target)
        {
            var parts = target.Split('/', '\\');
            var last = parts.Last();
            if (last.Contains(".")) return target;
            return target + ".obj";
        }

        public static string CanonicalArgument(string arg)
        {
            if (arg.StartsWith("-"))
                arg = "/" + arg.Substring(1);
            return arg;
        }

        /// <summary>
        /// Given a string we expect is a disk path, swap "/" to "\".
        /// </summary>
        /// <remarks>
        /// This might be from running under cygwin or mingw, cl is quite lax about this.
        /// </remarks>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string MakeWindowsPath(string path)
        {
            return path.Replace('/', '\\');            
        }

        public static IEnumerable<string> FixupArgs(IEnumerable<string> args)
        {
            var rv = new List<string>();
            var aa = args.ToArray();
            for (int i = 0; i < aa.Length; i++)
            {
                var a = aa[i];
                if ( CanonicalArgument(a) == "/D" )
                {
                    string val;
                    if (a.Length == 2 && (i + 1 < aa.Length))
                    {
                        val = aa[++i];
                    }
                    else
                    {
                        val = a.Substring(2);
                    }

                    if (val.Contains("=\""))
                    {
                        val = Regex.Replace(val, "\"", "\"\"\"");
                    }

                    rv.Add("/D" + val);
                }
                else
                {
                    rv.Add(a);
                }
            }

            return rv;
        }
    }
}
