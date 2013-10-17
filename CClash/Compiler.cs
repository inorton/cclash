using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace CClash
{
    public class Compiler
    {
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
                        var f = Path.GetFileNameWithoutExtension(SourceFile) + ".pdb";
                        PdbFile = Path.Combine(Environment.CurrentDirectory, f);
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

        public int InvokeCompiler(IEnumerable<string> args, StringBuilder stderr, StringBuilder stdout)
        {
            var envs = Environment.GetEnvironmentVariables();
            var cla = JoinAguments(args);
            var psi = new ProcessStartInfo(CompilerExe, cla)
            {
                UseShellExecute = false,
                //RedirectStandardError = true,
                //RedirectStandardOutput = true, 
                WorkingDirectory = Environment.CurrentDirectory,
                WindowStyle = ProcessWindowStyle.Normal,
                CreateNoWindow = false,
                ErrorDialog = true,
            };

            foreach (System.Collections.DictionaryEntry de in envs)
            {
                psi.EnvironmentVariables[(string)de.Key] = (string)de.Value;
            }
            psi.EnvironmentVariables["PATH"] = Path.GetDirectoryName(CompilerExe) + ";" + psi.EnvironmentVariables["PATH"];
            psi.ErrorDialog = true;
            var p = Process.Start(psi);
            p.WaitForExit();
            //stdout.Append(p.StandardOutput.ReadToEnd());
            //stderr.Append(p.StandardError.ReadToEnd());
            
            return p.ExitCode;
        }
             
    }
}
