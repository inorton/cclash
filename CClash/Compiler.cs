using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public class Compiler
    {
        public string[] CommandLine { get; set; }

        public string SourceFile { get; set; }
        public string ObjectFile { get; set; }
        public string PdbFile { get; set; }

        public bool Linking { get; set; }
        public bool PrecompiledHeaders { get; set; }
        public bool SingleSource { get; set; }

        public bool IsSupported
        {
            get
            {
                return (!Linking &&
                    !PrecompiledHeaders &&
                    SingleSource &&
                    !String.IsNullOrWhiteSpace(SourceFile) &&
                    !String.IsNullOrWhiteSpace(ObjectFile) 
                    );
            }
        }

        public void ProcessArguments(string[] args)
        {
            CommandLine = args;
        }
    }
}
