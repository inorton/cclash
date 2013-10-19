using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;

namespace CClash
{
    public sealed class Compiler
    {
        static Regex findLineInclude = new Regex("#line\\s+\\d+\\s+\"([^\"]+)\"");

        public Compiler()
        {
            CompilerExe = "cl";
        }

        public string CompilerExe { get; set; }

        public string[] CommandLine { get; set; }

        public string SourceFile { 
            get {
                return srcs.FirstOrDefault();
            }
        }
        public string ObjectTarget { get; set; }
        public string PdbFile { get; set; }

        public bool Linking { get; set; }
        public bool PrecompiledHeaders { get; set; }
        public bool GeneratePdb { get; set; }
        public bool ResponseFile { get; set; }

        List<string> srcs = new List<string>();
        List<string> incs = new List<string>();
        List<string> cliincs = new List<string>();

        public bool SingleSource
        {
            get
            {
                return srcs.Count == 1;
            }
        }

        public bool IsSupported
        {
            get
            {
                return (!Linking &&
                    !PrecompiledHeaders &&
                    SingleSource &&
                    !String.IsNullOrWhiteSpace(SourceFile) &&
                    !String.IsNullOrWhiteSpace(ObjectTarget) &&
                    File.Exists(SourceFile)
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
            if (arg.StartsWith("-") || arg.StartsWith("/"))
            {
                return "/" + arg.Substring(1);
            }
            return arg;
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
                    
                    switch (opt)
                    {
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
                            if (File.Exists(srcfile))
                            {
                                srcs.Add(srcfile);
                            }
                            else
                            {
                                return false;
                            }
                            break;

                        default:
                            if (full == "/link") Linking = true;

                            if (!full.StartsWith("/"))
                            {
                                if (File.Exists(full))
                                {
                                    srcs.Add(full);
                                }
                            }
                            if (!full.StartsWith("/I"))
                            {
                                var d = full.Substring(2);
                                if (Directory.Exists(d))
                                    cliincs.Add(d);
                            }

                            if (opt.StartsWith("@"))
                            {
                                ResponseFile = true;
                                return false;
                            }
                            break;
                    }
                }
                if (SingleSource)
                {
                    if (ObjectTarget == null)
                    {

                        var f = Path.GetFileNameWithoutExtension(SourceFile) + ".obj";
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
                            throw new InvalidProgramException("could not work out compiler version for auto generated pdb");
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

        public string JoinAguments(IEnumerable<string> args)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var a in args)
            {
                if (a.Contains(' '))
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
            var fullSrc = Path.GetFullPath(SourceFile);
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
                    if (!File.Exists(p))
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

        public int GetUsedIncludeFiles(IEnumerable<string> args, List<string> files, List<string>incdirs)
        {
            var xargs = new List<string>(args);
            xargs.Add("/E");
            var hashlines = new List<string>(200);
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
            incdirs.Add(Path.GetFullPath( Path.GetDirectoryName(SourceFile)));

            var rv = InvokeCompiler(xargs, null, x => hashlines.Add(x), true);
            if (rv == 0)
            {
                foreach (var l in hashlines)
                {
                    var tmp = l.Substring(1+l.IndexOf('"'));
                    if (tmp != null)
                    {
                        tmplist.Add(Path.GetFullPath(tmp.TrimEnd('"')));
                    }
                }
                files.AddRange(tmplist.Distinct());
            }
            return rv;
        }

        public int InvokeCompiler(IEnumerable<string> args, Action<string> onStdErr, Action<string> onStdOut, bool onlyCaptureLineDirectives)
        {
            var envs = Environment.GetEnvironmentVariables();
            var cla = JoinAguments(args);
            var psi = new ProcessStartInfo(CompilerExe, cla)
            {
                UseShellExecute = false,
                RedirectStandardError = !onlyCaptureLineDirectives,
                RedirectStandardOutput = true, 
                WorkingDirectory = Environment.CurrentDirectory,
            };

            psi.EnvironmentVariables["PATH"] = Path.GetDirectoryName(CompilerExe) + ";" + psi.EnvironmentVariables["PATH"];
            psi.ErrorDialog = true;
            var p = Process.Start(psi);

            p.OutputDataReceived += (o, a) =>
            {

                if ( a.Data != null &&( !onlyCaptureLineDirectives || a.Data.StartsWith("#line")))
                    onStdOut(a.Data);
            };

            if (!onlyCaptureLineDirectives)
            {
                p.ErrorDataReceived += (o, a) =>
                {
                    if ( a.Data != null ) onStdErr(a.Data);
                };

                p.BeginErrorReadLine();
            }

            p.BeginOutputReadLine();

            p.WaitForExit();
                        
            return p.ExitCode;
        }
             
    }
}
