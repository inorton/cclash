using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
        static extern int GetLongPathName(
            string path,
            StringBuilder longPath,
            int longPathLength
            );

        /// <summary>
        /// Create a new instance of the Compiler class.
        /// </summary>
        public Compiler()
        {
            CompilerExe = "cl";
        }

        private string compilerExe;

        /// <summary>
        /// The real compiler we've been told to use.
        /// </summary>
        public string CompilerExe
        {
            get { return compilerExe; }
            set { 
                compilerExe = value;
                if (Path.IsPathRooted(compilerExe) && compilerExe.Contains("~"))
                {
                    var sb = new StringBuilder();
                    GetLongPathName(compilerExe, sb, sb.Capacity);
                    compilerExe = sb.ToString();
                }
            }
        }

        /// <summary>
        /// The command line supplied to us.
        /// </summary>
        public string[] CommandLine { get; set; }

        /// <summary>
        /// The first source file.
        /// </summary>
        public string SingleSourceFile { 
            get {
                return srcs.FirstOrDefault();
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
                    !String.IsNullOrWhiteSpace(SingleSourceFile) &&
                    !String.IsNullOrWhiteSpace(ObjectTarget) &&
                    FileUtils.Exists(SingleSourceFile)
                    );
            }
        }

        string getOption( string arg )
        {
            if ( arg.StartsWith("-") || arg.StartsWith("/") ){
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
                                    return false;
                                }
                                full = "/I" + args[i];
                                goto default;
                            }
                            break;

                        case "/Yu":
                            PrecompiledHeaders = true;
                            return false;

                        case "/FI":
                            return false;

                        case "/Zi":
                            GeneratePdb = true;
                            break;
                        case "/Fd":
                            PdbFile = Path.Combine( Environment.CurrentDirectory, full.Substring(3));
                            if (!Path.GetFileName(PdbFile).Contains("."))
                                PdbFile += ".pdb";
                            break;
                        
                        case "/Fo":
                            ObjectTarget = Path.Combine(Environment.CurrentDirectory, full.Substring(3));
                            if (!Path.GetFileName(ObjectTarget).Contains("."))
                                ObjectTarget += ".obj";
                            break;

                        case "/Tp":
                        case "/Tc":
                            var srcfile = full.Substring(3);
                            if (FileUtils.Exists(srcfile))
                            {
                                srcs.Add(srcfile);
                            }
                            else
                            {
                                return false;
                            }
                            break;

                        default:
                            if (full == "/link")
                            {
                                Linking = true;
                                return false;
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
                                        args = CommandLineToArgs(rsptxt).ToArray();
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

                                return false;
                            }

                            if (!full.StartsWith("/"))
                            {
                                if (FileUtils.Exists(full))
                                {
                                    srcs.Add(full);
                                    continue;
                                }
                            }
                            if (full.StartsWith("/I"))
                            {
                                var d = full.Substring(2);
                                if (Directory.Exists(d))
                                {
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
                        ObjectTarget = Path.Combine(Environment.CurrentDirectory, f);
                    }
                    if (GeneratePdb && PdbFile == null)
                    {
                        for ( int x = 14; x > 8; x-- )
                        {
                            if ( CompilerExe.Contains(string.Format("Microsoft Visual Studio {0}.0",x)) )
                            {
                                var f = string.Format("vc{0}0.pdb", x); 
                                PdbFile = Path.Combine(Environment.CurrentDirectory, f);
                                break;
                            }
                        }
                        if ( PdbFile == null ) {
                            Logging.Emit("could not work out compiler version for auto generated pdb");
                            return false;
                        }
                    }
                }   
                     
            }
            catch ( Exception e )
            {
                Console.Error.WriteLine(e);
                return false;
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
                foreach (var f in ( from x in incfiles where x.StartsWith(d, StringComparison.CurrentCultureIgnoreCase) select x ) )
                {
                    if (f != fullSrc)
                    {
                        var incpath = f.Substring(d.Length);
                        includelines.Add(incpath.TrimStart('\\'));
                    }
                }
            }

            HashSet<string> tmp = new HashSet<string>( includelines );
            foreach (var y in tmp)
            {
                foreach (var x in incdirs)
                {
                    var p = Path.Combine( x, y );
                    if (!FileUtils.Exists(p))
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
            if (iinc != null)
            {
                incs.Clear();
                incs.AddRange(cliincs);
                foreach (var i in iinc.Split(';'))
                {
                    incs.Add(i);
                }
                incdirs.AddRange(incs);
            }
            incdirs.Add(Path.GetFullPath( Path.GetDirectoryName(SingleSourceFile)));
            return incdirs;
        }

        public int InvokeCompiler(IEnumerable<string> args, Action<string> onStdErr, Action<string> onStdOut, bool showIncludes, List<string> foundIncludes)
        {
            if (!FileUtils.Exists(CompilerExe))
                throw new FileNotFoundException("cant find cl.exe");

            var envs = Environment.GetEnvironmentVariables();
            var cla = JoinAguments(args);
            if (showIncludes) cla += " /showIncludes";
            var psi = new ProcessStartInfo(CompilerExe, cla)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true, 
                WorkingDirectory = Environment.CurrentDirectory,
            };

            psi.EnvironmentVariables["PATH"] = Path.GetDirectoryName(CompilerExe) + ";" + psi.EnvironmentVariables["PATH"];
            psi.ErrorDialog = true;
            var p = Process.Start(psi);

            p.OutputDataReceived += (o, a) =>
            {
                if ( a.Data != null ) {
                    if (showIncludes)
                    {
                        if (a.Data.StartsWith("Note: including file:"))
                        {
                            var inc = a.Data.Substring("Note: including file:".Length+1);
                            foundIncludes.Add( inc.TrimStart(' ') );
                        }
                        else
                        {
                            onStdOut(a.Data);
                        }
                    }
                    else
                    {
                        onStdOut(a.Data);
                    }
                }
                    
            };

            p.ErrorDataReceived += (o, a) =>
            {
                  if (a.Data != null) onStdErr(a.Data);
            };

            p.BeginErrorReadLine();
            
            p.BeginOutputReadLine();

            p.WaitForExit();
                        
            return p.ExitCode;
        }
             
    }
}
