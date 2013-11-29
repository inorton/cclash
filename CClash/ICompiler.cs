using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CClash
{
    public interface ICompiler
    {
        System.Collections.Generic.List<string> CliIncludePaths { get; }
        string[] CommandLine { get; set; }
        string CompilerExe { get; set; }
        Dictionary<string, string> Envs { get; }
        IEnumerable<string> FixupArgs(IEnumerable<string> args);
        bool GeneratePdb { get; set; }
        string GetPath(string path);
        List<string> GetPotentialIncludeFiles(IEnumerable<string> incdirs, IEnumerable<string> incfiles);
        List<string> GetUsedIncludeDirs(List<string> files);
        int InvokeCompiler(IEnumerable<string> args, Action<string> onStdErr, Action<string> onStdOut, bool showIncludes, List<string> foundIncludes);
        int InvokePreprocessor(System.IO.StreamWriter stdout);
        bool Linking { get; set; }
        string ObjectTarget { get; set; }
        string PdbFile { get; set; }
        bool PrecompiledHeaders { get; set; }
        bool ProcessArguments(string[] args);
        string ResponseFile { get; set; }
        bool SingleSource { get; }
        string SingleSourceFile { get; }
        string[] SourceFiles { get; }
        string WorkingDirectory { get; }
    }
}
