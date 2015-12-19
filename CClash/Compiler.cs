using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CClash
{
    /// <summary>
    /// Class for processing compiler inputs, running the compiler and deducing outputs.
    /// </summary>
    public sealed class Compiler : CClash.ICompiler
    {
        static Regex findLineInclude = new Regex("#line\\s+\\d+\\s+\"([^\"]+)\"");
        public const string InternalResponseFileSuffix = "cclash";

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static unsafe extern IntPtr GetEnvironmentStringsA();


        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        static extern int CreateHardLink(
        string lpFileName,
        string lpExistingFileName,
        IntPtr lpSecurityAttributes
        );

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
            if (!Settings.IsCygwin)
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
            var compiler = _Find();
            Logging.Emit("chose compiler {}", compiler);
            return compiler;
        }

        static string _Find()
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
            Created = DateTime.Now;
        }

        public DateTime Created { get; set; }

        public TimeSpan Age
        {
            get
            {
                return DateTime.Now.Subtract(Created);
            }
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
        /// The arguments that should be sent to the compiler by us or our caller.
        /// </summary>
        public string[] CompileArgs { get; set; }

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

        public bool HasDashC
        {
            get;
            private set;
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

        public int ParentPid { get; set; }

        public string ObjectTarget { get; set; }
        public string PdbFile { get; set; }

        public bool Linking { get; set; }
        public bool PrecompiledHeaders { get; set; }
        public bool GeneratePdb { get; set; }
        public bool AttemptPdb { get; set; }
        public bool PdbExistsAlready { get; set; }
        public bool ObjectTargetIsFile { get; set; }
        public string ResponseFile { get; set; }

        public Action<string> StdErrorCallback { get; set; }
        public Action<string> StdOutputCallback { get; set; }

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
                return (
                    HasDashC &&
                    !Linking &&
                    !PrecompiledHeaders &&
                    SingleSource &&
                    ((!GeneratePdb) || AttemptPdb ) &&
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
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException("path " + path);
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
            Logging.Emit("args: {0}", string.Format(" ", CommandLine));
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
                    var opt = getOption(args[i]);
                    var full = getFullOption(args[i]);

                    #region switch process each argument type
                    switch (opt)
                    {
                        case "/c":
                            HasDashC = true;
                            break;
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
                                    return NotSupported("-I has no path!");
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
                            PdbFile = Path.Combine(WorkingDirectory, full.Substring(3));
                            // openssl gives us a posix path here..
                            PdbFile = PdbFile.Replace('/', '\\');
                            if (!PdbFile.ToLower().EndsWith(".pdb") && !PdbFile.EndsWith("\\"))
                            {
                                PdbFile = PdbFile + ".pdb";
                            }
                            break;

                        case "/Fo":
                            ObjectTarget = Path.Combine(WorkingDirectory, full.Substring(3));
                            ObjectTarget = ObjectTarget.Replace('/', '\\');
                            if (!Path.GetFileName(ObjectTarget).Contains(".") && !ObjectTarget.EndsWith("\\"))
                                ObjectTarget += ".obj";
                            ObjectTargetIsFile = ObjectTarget.EndsWith(".obj");
                            break;

                        case "/Tp":
                        case "/Tc":
                            var srcfile = full.Substring(3);
                            if (!Path.IsPathRooted(srcfile))
                                srcfile = Path.Combine(WorkingDirectory, srcfile);

                            if (FileUtils.Exists(srcfile))
                            {
                                srcs.Add(srcfile);
                            }
                            else
                            {
                                return NotSupported("cant find file for {0}", full);
                            }
                            break;

                        case "/E":
                            return NotSupported(opt);

                        case "/EP":
                            return NotSupported(opt);

                        default:
                            #region positional or other flag options

                            if (full == "/link")
                            {
                                Linking = true;
                                return NotSupported("/link");
                            }

                            if (opt.StartsWith("@"))
                            {
                                #region response file
                                ResponseFile = full.Substring(1);

                                if (ResponseFile.EndsWith(InternalResponseFileSuffix))
                                {
                                    Logging.Emit("cclash misshelper internal response file");
                                    return false;
                                }

                                if (!Path.IsPathRooted(ResponseFile))
                                    ResponseFile = Path.Combine(WorkingDirectory, ResponseFile);
                                string rsptxt = File.ReadAllText(ResponseFile);
                                if (rsptxt.Length < 2047)
                                // windows max command line, this is why they invented response files
                                {
                                    Logging.Emit("response data [{0}]", rsptxt);
                                    if (args.Length == 1)
                                    {
                                        // this only works if it is the one and only arg!
                                        args = FixupArgs( CommandLineToArgs(rsptxt).Skip(1) ).ToArray();
                                        i = -1;
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
                                #endregion
                            }

                            if (!full.StartsWith("/"))
                            {
                                // NOTE, if we ever cache -link calls this will also match input objects and libs
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

                                if (!Path.IsPathRooted(d))
                                {
                                    d = Path.Combine(WorkingDirectory, d);
                                }

                                if (Directory.Exists(d))
                                {
                                    cliincs.Add(d);
                                    continue;
                                }
                            }
                            #endregion

                            break;
                    }
                    #endregion

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
                    else
                    {
                        if (!ObjectTargetIsFile)
                        {
                            ObjectTarget = Path.Combine(ObjectTarget, Path.GetFileNameWithoutExtension(SingleSourceFile)) + ".obj";
                        }
                    }

                    if (GeneratePdb)
                    {
                        if (Settings.ConvertObjPdbToZ7)
                        {
                            bool doconvert = false;
                            if (PdbFile == null)
                            {
                                doconvert = true;
                            }
                            else
                            {
                                if (PdbFile.EndsWith("\\"))
                                {
                                    doconvert = true;
                                }
                                else
                                {
                                    if (!FileUtils.Exists(PdbFile))
                                    {
                                        if (WorkingDirectory.Contains("openssl"))
                                        {
                                            doconvert = true;
                                        }
                                    }
                                }
                            }

                            if (doconvert)
                            {
                                Logging.Emit("converting pdb request to Z7 embedded debug {0}:{1}", WorkingDirectory, Path.GetFileName(ObjectTarget));
                                // append /Z7 to the arg list
                                var newargs = new List<string>();
                                foreach (var a in args)
                                {
                                    if (!(a.StartsWith("/Zi") || a.StartsWith("/Fd")))
                                    {
                                        newargs.Add(a);
                                    }
                                }
                                newargs.Add("/Z7");
                                AttemptPdb = false;
                                PdbFile = null;
                                GeneratePdb = false;
                                PdbExistsAlready = false;
                                args = newargs.ToArray();
                            }
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
            CompileArgs = args.ToArray();
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
            string iinc = null;

            EnvironmentVariables.TryGetValue("INCLUDE", out iinc);
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
            return InvokeCompiler(xargs, (x) => { }, stdout.Write, false, null);
        }

        public int InvokeCompiler(IEnumerable<string> args, Action<string> onStdErr, Action<string> onStdOut, bool showIncludes, List<string> foundIncludes)
        {
            int rv = -1;
            bool retry;
            do
            {
                retry = false;
                Logging.Emit("invoking real compiler: {0} {1} [{2}]", CompilerExe, WorkingDirectory, string.Join(" ", args.ToArray()));

                if (string.IsNullOrWhiteSpace(CompilerExe) || !FileUtils.Exists(CompilerExe))
                    throw new FileNotFoundException("cant find cl.exe");

                if (string.IsNullOrWhiteSpace(compworkdir))
                    throw new InvalidOperationException("no working directory set");

                if (compenvs == null || compenvs.Count == 0)
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
                            if (StdOutputCallback != null) {
                                StdOutputCallback(a.Data + Environment.NewLine);
                            }
                            if (onStdOut != null) {
                                onStdOut(a.Data + Environment.NewLine);
                            }
                            if (Settings.DebugEnabled)
                                Logging.Emit("stdout {0}", a.Data);
                        }
                    }

                };

                p.ErrorDataReceived += (o, a) =>
                {
                    if (a.Data != null)
                    {
                        if (StdErrorCallback != null) {
                            StdErrorCallback(a.Data + Environment.NewLine);
                        }
                        if (onStdErr != null) {
                            onStdErr(a.Data + Environment.NewLine);
                        }
                        if (Settings.DebugEnabled)
                            Logging.Emit("stderr {0}", a.Data);
                    }
                };

                p.BeginErrorReadLine();
                p.BeginOutputReadLine();

                p.WaitForExit();


                rv = p.ExitCode;
                p.Close();
                Logging.Emit("cl exit {0}", rv);
                if (rv == 0)
                {
                    if (IsSupported)
                    {
                        if (!string.IsNullOrEmpty(ObjectTarget))
                        {
                            var sw = new Stopwatch();
                            sw.Start();
                            int waited = 0;

                            while (!File.Exists(ObjectTarget) && (sw.ElapsedMilliseconds < 10000))
                            {
                                Logging.Emit("compiler slow to write object! {0} {1}ms", ObjectTarget, (int)sw.Elapsed.TotalMilliseconds);
                                waited++;
                                System.Threading.Thread.Sleep(Math.Min(1000, 100 * waited));
                            }

                            if (!File.Exists(ObjectTarget))
                            {
                                string logmsg = string.Format("cl exited with zero but failed to create the object file! {0}", ObjectTarget);
                                // let the retry system have a go with this

                                Logging.Warning("{0}, re-running!", logmsg);
                                retry = true;
                            }
                            Logging.Emit("output: {0} seen", ObjectTarget);
                        }
                    }
                }

                if (rv != 0)
                {
                    Logging.Emit("non-zero exit");
                }
            } while (retry);
            return rv;
        }

    }
}
