using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CClash
{
    /// <summary>
    /// Class for processing compiler inputs, running the compiler and deducing outputs.
    /// </summary>
    public sealed class Compiler
    {
        static Regex findLineInclude = new Regex("#line\\s+\\d+\\s+\"([^\"]+)\"");

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static unsafe extern IntPtr GetEnvironmentStringsA();


        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        static extern int CreateHardLink(
        string lpFileName,
        string lpExistingFileName,
        IntPtr lpSecurityAttributes
        );

        public static bool MakeHardLink(string to, string from)
        {
            return !FileUtils.Exists(to) && (CreateHardLink(to, from, IntPtr.Zero) != 0);
        }

        static Compiler()
        {
            cygwinEnvFixup();
        }

        public static Dictionary<string, string> GetEnvironmentDictionary()
        {
            var rv = new Dictionary<string, string>();
            var envs = Environment.GetEnvironmentVariables();
            foreach (string name in envs.Keys)
            {
                rv[name.ToUpper()] = string.Format("{0}", envs[(object)name]);
            }

            return rv;
        }

        public static Dictionary<string, string> FixEnvironmentDictionary(Dictionary<string, string> envs)
        {
            if (envs == null) throw new ArgumentNullException("envs");
            var rv = new Dictionary<string,string>();
            foreach (var row in envs)
            {
                rv[row.Key.ToUpper()] = row.Value;
            }

            return rv;
        }

        static void cygwinEnvFixup()
        {
            if (Environment.GetEnvironmentVariable("NO_CCLASH_CYGWIN_FIX") != null)
                return;

            List<string> lines = new List<string>();
            unsafe
            {
                var ppenvs = GetEnvironmentStringsA();
                List<byte> buf = new List<byte>();

                byte* envs = (byte*)ppenvs.ToPointer();

                for (int i = 0; true; i++)
                {
                    if (envs[i] == (byte)0)
                    {
                        lines.Add(System.Text.Encoding.ASCII.GetString(buf.ToArray()));
                        buf.Clear();
                        if (envs[i + 1] == (byte)0)
                        {
                            break; // end of buffer. yuk..
                        }
                    }
                    else
                    {
                        buf.Add(envs[i]);
                    }
                }
                Marshal.FreeHGlobal(ppenvs);
            }

            foreach (var e in lines)
            {
                var pair = e.Split(new char[] { '=' }, 2);
                var haslow = false;
                foreach (var c in pair[0])
                {
                    if (char.IsLower(c))
                    {
                        haslow = true;
                        break;
                    }
                }

                if (haslow)
                {
                    Environment.SetEnvironmentVariable(pair[0], null);
                    Environment.SetEnvironmentVariable(pair[0].ToUpper(), pair[1]);
                }
            }

        }

        public static string Find()
        {
            var compiler = Environment.GetEnvironmentVariable("CCLASH_CL");
            if ((compiler != null) && File.Exists(compiler)) return compiler;

            var self = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            var path = Environment.GetEnvironmentVariable("PATH");
            var paths = path.Split(';');

            var selfdir = Path.GetDirectoryName(self);
            var realcl = Path.Combine(selfdir, "cl_real.exe");
            if (File.Exists(realcl)) return realcl;

            foreach (var p in paths)
            {
                var f = Path.Combine(p, "cl.exe");
                if (FileUtils.Exists(f))
                {
                    if (f.Equals(self, StringComparison.CurrentCultureIgnoreCase))
                    {
                        continue;
                    }
                    if (Path.IsPathRooted(f))
                    {
                        return f;
                    }
                }
            }

            return null;
        }


        /// <summary>
        /// Create a new instance of the Compiler class.
        /// </summary>
        public Compiler()
        {
            compilerExe = "cl";
        }

        private string compilerExe;

        /// <summary>
        /// The real compiler we've been told to use.
        /// </summary>
        public string CompilerExe
        {
            get { return compilerExe; }
            set
            {
                compilerExe = FileUtils.ToLongPathName(value);
                Logging.Emit("real compiler is: {0}", compilerExe);
            }
        }

        /// <summary>
        /// The command line supplied to us.
        /// </summary>
        public string[] CommandLine { get; set; }

        /// <summary>
        /// The first source file.
        /// </summary>
        public string SingleSourceFile
        {
            get
            {
                return srcs.FirstOrDefault();
            }
        }

        public string AbsoluteSourceFile
        {
            get
            {
                var single = SingleSourceFile;
                if (!string.IsNullOrWhiteSpace(single))
                {
                    if (!Path.IsPathRooted(single))
                    {
                        single = Path.Combine(WorkingDirectory, single);
                    }
                }
                return single;
            }
        }

        /// <summary>
        /// The full list of source files.
        /// </summary>
        /// <remarks>
        /// CClash does not currently support caching multiple source file invocations.
        /// </remarks>
        public string[] SourceFiles
        {
            get
            {
                return srcs.ToArray();
            }
        }

        public string ObjectTarget { get; set; }
        public string PdbFile { get; set; }

        public bool Linking { get; set; }
        public bool PrecompiledHeaders { get; set; }
        public bool GeneratePdb { get; set; }
        public string ResponseFile { get; set; }

        List<string> srcs = new List<string>();
        List<string> incs = new List<string>();
        List<string> cliincs = new List<string>();

        public List<string> CliIncludePaths
        {
            get
            {
                return new List<string>(cliincs);
            }
        }

        public bool SingleSource
        {
            get
            {
                return srcs.Count == 1;
            }
        }

        bool IsSupported
        {
            get
            {
                return (!Linking &&
                    !PrecompiledHeaders &&
                    SingleSource &&
                    !GeneratePdb &&
                    !String.IsNullOrWhiteSpace(SingleSourceFile) &&
                    !String.IsNullOrWhiteSpace(ObjectTarget) &&
                    FileUtils.Exists(AbsoluteSourceFile)
                    );
            }
        }

        string getOption(string arg)
        {
            if (arg.StartsWith("-") || arg.StartsWith("/"))
            {
                var rv = "/" + arg.Substring(1);
                if (rv.Length > 2) rv = rv.Substring(0, 3);
                return rv;
            }
            return arg;
        }

        string getFullOption(string arg)
        {
            arg = arg.Trim('"', '\'');
            if (arg.StartsWith("-") || arg.StartsWith("/"))
            {
                return "/" + arg.Substring(1);
            }
            return arg;
        }

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);

        public static List<string> CommandLineToArgs(string commandLine)
        {
            int argc;
            var argv = CommandLineToArgvW("cl.exe " + commandLine, out argc);
            if (argv == IntPtr.Zero)
                throw new InvalidProgramException("could not split command line args");

            var args = new List<string>();
            for (var i = 0; i < argc; i++)
            {
                var pstr = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                args.Add(Marshal.PtrToStringUni(pstr));
            }
            Marshal.FreeHGlobal(argv);
            return args;
        }

        public IEnumerable<string> FixupArgs(IEnumerable<string> args)
        {
            var rv = new List<string>();
            var aa = args.ToArray();
            for (int i = 0; i < aa.Length; i++)
            {
                var a = aa[i];
                if (a.StartsWith("/D") || a.StartsWith("-D"))
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

        private Dictionary<string, string> compenvs;

        public void SetEnvironment(Dictionary<string, string> envs)
        {
            compenvs = FixEnvironmentDictionary(envs);
        }

        public Dictionary<string, string> EnvironmentVariables 
        {
            get
            {
                return compenvs;
            }
        }

        string compworkdir;

        public void SetWorkingDirectory(string path)
        {
            if (path == null) throw new ArgumentNullException("path");
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException("path");
            compworkdir = path;
        }

        public string WorkingDirectory
        {
            get
            {
                return compworkdir;
            }
        }

        bool NotSupported(string fmt, params object[] args)
        {
            Logging.Emit("argument not supported : {0}", string.Format( fmt, args));
            return false;
        }

        public bool ProcessArguments(string[] args)
        {
            try
            {
                CommandLine = args;
                for (int i = 0; i < args.Length; i++)
                {
                    Logging.Emit("process arg '{0}'", args[i]);
                    var opt = getOption(args[i]);
                    var full = getFullOption(args[i]);

                    switch (opt)
                    {
                        case "/o":
                            return NotSupported("/o");
                        case "/D":
                            if (opt == full)
                            {
                                // define value is next argument...
                                i++;
                            }
                            break;
                        case "/I":
                            if (opt == full)
                            {
                                // include path is next argument..
                                // microsoft really dont know how to do command line!
                                i++;
                                if (i > args.Length)
                                {
                                    return NotSupported("-I has not path!");
                                }
                                full = "/I" + args[i];
                                goto default;
                            }
                            break;

                        case "/Z7":
                            GeneratePdb = false;
                            PdbFile = null;
                            break;

                        case "/Yu":
                            PrecompiledHeaders = true;
                            return NotSupported("pre-compiler headers {0}", opt);

                        case "/FI":
                            return NotSupported(opt);

                        case "/Zi":
                            GeneratePdb = true;
                            break;
                        case "/Fd":
                            GeneratePdb = true;
                            break;

                        case "/Fo":
                            ObjectTarget = Path.Combine(WorkingDirectory, full.Substring(3));
                            if (!Path.GetFileName(ObjectTarget).Contains("."))
                                ObjectTarget += ".obj";
                            break;

                        case "/Tp":
                        case "/Tc":
                            var srcfile = full.Substring(3);
                            if (FileUtils.Exists(srcfile))
                            {
                                srcs.Add(Path.GetFullPath(srcfile));
                            }
                            else
                            {
                                return NotSupported("cant find file for {0}", full);
                            }
                            break;

                        default:

                            if (full.StartsWith("/E"))
                            {
                                return NotSupported("/E");
                            }

                            if (full == "/link")
                            {
                                Linking = true;
                                return NotSupported("/link");
                            }

                            if (opt.StartsWith("@"))
                            {
                                ResponseFile = full.Substring(1);
                                var rsptxt = File.ReadAllText(opt.Substring(1));
                                if (rsptxt.Length < 2047)
                                // windows max command line, this is why they invented response files
                                {
                                    Logging.Emit("response data [{0}]", rsptxt);
                                    if (args.Length == 1)
                                    {
                                        // this only works if it is the one and only arg!
                                        args = CommandLineToArgs(rsptxt).Skip(1).ToArray();
                                        i = 0;

                                        // replace the command line with the response file content 
                                        // and restart parsing. This does go wrong if the response text is huge
                                        continue;
                                    }
                                }
                                else
                                {
                                    Logging.Emit("response file too large");
                                }

                                return NotSupported("response file error");
                            }

                            if (!full.StartsWith("/"))
                            {
                                var file = full;
                                if (!Path.IsPathRooted(file))
                                    file = Path.Combine(WorkingDirectory, file);

                                if (FileUtils.Exists(file))
                                {
                                    srcs.Add(file);
                                    continue;
                                }
                            }
                            if (full.StartsWith("/I"))
                            {
                                var d = full.Substring(2);
                                if (d == ".")
                                    d = WorkingDirectory;
                                if (d == "..")
                                    d = Path.GetDirectoryName(WorkingDirectory);

                                if (Directory.Exists(d))
                                {
                                    Logging.Emit("cli include '{0}' => {1}", full, d);
                                    cliincs.Add(d);
                                    continue;
                                }
                            }

                            break;
                    }
                }
                if (SingleSource)
                {
                    if (ObjectTarget == null)
                    {
                        var f = Path.GetFileNameWithoutExtension(SingleSourceFile) + ".obj";
                        if (Path.IsPathRooted(f))
                        {
                            ObjectTarget = f;
                        }
                        else
                        {
                            ObjectTarget = Path.Combine(WorkingDirectory, f);
                        }
                    }

                    if (GeneratePdb)
                    {
                        return NotSupported("PDB file requested");
                    }

                }

            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return NotSupported("option parser exception '{0}'", e);
            }

            return IsSupported;
        }

        public static string JoinAguments(IEnumerable<string> args)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var a in args)
            {
                if (a.Contains(' ') || a.Contains('\t'))
                {
                    sb.AppendFormat("\"{0}\"", a);
                }
                else
                {
                    sb.Append(a);
                }
                sb.Append(" ");
            }
            return sb.ToString().TrimEnd();
        }

        public List<string> GetPotentialIncludeFiles(IEnumerable<string> incdirs, IEnumerable<string> incfiles)
        {
            List<string> possibles = new List<string>();
            List<string> includelines = new List<string>();
            var fullSrc = Path.GetFullPath(SingleSourceFile);
            foreach (var d in incdirs)
            {
                foreach (var f in (from x in incfiles where x.StartsWith(d, StringComparison.CurrentCultureIgnoreCase) select x))
                {
                    if (f != fullSrc)
                    {
                        var incpath = f.Substring(d.Length);
                        includelines.Add(incpath.TrimStart('\\'));
                    }
                }
            }

            HashSet<string> tmp = new HashSet<string>(includelines);
            foreach (var y in tmp)
            {
                foreach (var x in incdirs)
                {
                    var p = Path.Combine(x, y);
                    if (FileUtils.FileMissing(p))
                    {
                        possibles.Add(p);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return possibles;
        }

        public List<string> GetUsedIncludeDirs(List<string> files)
        {
            var incdirs = new List<string>();
            var tmplist = new List<string>(1000);
            var iinc = Environment.GetEnvironmentVariable("INCLUDE");
            Logging.Emit("INCLUDE={0}", iinc);
            if (iinc != null)
            {
                incs.Clear();
                incs.AddRange(cliincs);
                foreach (var i in iinc.Split(';'))
                {
                    incs.Add(i);
                    Logging.Emit("notice include folder: {0}", i);
                }
                incdirs.AddRange(incs);
            }
            var srcfolder = Path.GetDirectoryName(SingleSourceFile);
            if (string.IsNullOrEmpty(srcfolder))
                srcfolder = WorkingDirectory;
            Logging.Emit("notice source folder: {0}", srcfolder);
            incdirs.Add(Path.GetFullPath(srcfolder));
            return incdirs;
        }

        public int InvokePreprocessor(StreamWriter stdout)
        {
            var xargs = new List<string>();
            xargs.Add("/EP");
            xargs.AddRange(from x in CommandLine where (x != "/c" || x != "-c") select x);
            return InvokeCompiler(xargs, (x) => { }, stdout.WriteLine, false, null);
        }

        public int InvokeCompiler(IEnumerable<string> args, Action<string> onStdErr, Action<string> onStdOut, bool showIncludes, List<string> foundIncludes)
        {
            Logging.Emit("invoking real compiler: [{0}]", string.Join(" ", args.ToArray()));

            if (string.IsNullOrWhiteSpace(CompilerExe) || !FileUtils.Exists(CompilerExe))
                throw new FileNotFoundException("cant find cl.exe");

            if (string.IsNullOrWhiteSpace(compworkdir))
                throw new InvalidOperationException("no working directory set");

            if (compenvs == null || compenvs.Count == 0 )
                throw new InvalidOperationException("no environment set");

            var cla = JoinAguments(FixupArgs(args));
            if (showIncludes) cla += " /showIncludes";
            var psi = new ProcessStartInfo(CompilerExe, cla)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WorkingDirectory = compworkdir,
            };

            psi.EnvironmentVariables.Clear();
            foreach (var row in compenvs)
            {
                psi.EnvironmentVariables[row.Key] = row.Value;
            }
            psi.EnvironmentVariables["PATH"] = Path.GetDirectoryName(CompilerExe) + ";" + psi.EnvironmentVariables["PATH"];
            psi.ErrorDialog = true;
            var p = Process.Start(psi);

            p.OutputDataReceived += (o, a) =>
            {
                if (a.Data != null)
                {
                    if (showIncludes && a.Data.StartsWith("Note: including file:"))
                    {
                        var inc = a.Data.Substring("Note: including file:".Length + 1).TrimStart(' ');
                        if (inc.Contains('/'))
                        {
                            inc = inc.Replace('/', '\\');
                        }
                        foundIncludes.Add(inc);
                    }
                    else
                    {
                        if (onStdOut != null) onStdOut(a.Data);
                    }
                }

            };

            p.ErrorDataReceived += (o, a) =>
            {
                if (onStdErr != null)
                    if (a.Data != null)
                        onStdErr(a.Data);
            };

            p.BeginErrorReadLine();

            p.BeginOutputReadLine();

            p.WaitForExit();

            if (p.ExitCode == 0)
            {
                if (IsSupported) {
                    if (!string.IsNullOrEmpty(ObjectTarget)) {
                        bool missing = true;
                        var sw = new Stopwatch();
                        sw.Start();
                        do {
                            if (!FileUtils.Exists(ObjectTarget)) {
                                Logging.Emit("compiler slow to write object!");
                                System.Threading.Thread.Sleep(100);
                            } else {
                                missing = false;
                            }
                        } while (missing && sw.ElapsedMilliseconds < 2000);

                        if (missing) {
                            throw new CClashErrorException("compiler exited successfully but generated file '{0}' could not be not found", ObjectTarget);
                        }
                    }
                }
            }

            return p.ExitCode;
        }

    }
}
