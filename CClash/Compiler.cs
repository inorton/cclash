using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public class Compiler
    {
        public string[] CommandLine { get; set; }

        public string SourceFile { 
            get {
                return srcs.FirstOrDefault();
            }
        }
        public string ObjectTarget { get; set; }
        public bool ObjectTargetIsDirectory { get; set; }

        public string PdbFile { get; set; }

        public bool Linking { get; set; }
        public bool PrecompiledHeaders { get; set; }
        public bool GeneratePdb { get; set; }

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
                return arg.Substring(1);
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
                    switch (getOption(args[i]))
                    {
                        case "Yu":
                            PrecompiledHeaders = true;
                            return false;

                        case "link":
                            Linking = true;
                            return false;
                        case "Zi":
                            GeneratePdb = true;
                            break;
                        case "Fd":
                            PdbFile = args[++i];
                            break;
                        
                        case "Fo":
                            var tgt = args[++i];
                            ObjectTarget = tgt;
                            if (Directory.Exists(tgt))
                            {
                                ObjectTargetIsDirectory = true;
                            }
                            break;

                        case "Tp":
                        case "Tc":
                        case "c":
                            var srcfile = args[++i];
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
                            break;
                    }
                }

                if (SingleSource){
                    if (ObjectTargetIsDirectory)
                    {
                        ObjectTarget = Path.Combine( ObjectTarget, Path.GetFileName(SourceFile) );
                    }

                    if (ObjectTarget == null)
                    {
                        ObjectTarget = Path.Combine(Path.GetDirectoryName(SourceFile), Path.GetFileNameWithoutExtension(SourceFile)) + ".obj";
                    }
                     
                    if (GeneratePdb && string.IsNullOrEmpty(PdbFile))
                    {
                        PdbFile = Path.Combine(Path.GetDirectoryName(ObjectTarget), Path.GetFileNameWithoutExtension(ObjectTarget));
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
    }
}
