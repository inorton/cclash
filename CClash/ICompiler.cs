using System;
using System.Text;
namespace CClash
{
    public interface ICompiler
    {
        string AbsoluteSourceFile { get; }
        bool AttemptPdb { get; set; }
        System.Collections.Generic.List<string> CliIncludePaths { get; }
        string[] CommandLine { get; set; }
        string[] CompileArgs { get; set; }
        string CompilerExe { get; set; }
        System.Collections.Generic.Dictionary<string, string> EnvironmentVariables { get; }
        System.Collections.Generic.IEnumerable<string> FixupArgs(System.Collections.Generic.IEnumerable<string> args);
        bool GeneratePdb { get; set; }
        System.Collections.Generic.List<string> GetPotentialIncludeFiles(System.Collections.Generic.IEnumerable<string> incdirs, System.Collections.Generic.IEnumerable<string> incfiles);
        System.Collections.Generic.List<string> GetUsedIncludeDirs(System.Collections.Generic.List<string> files);
        bool HasDashC { get; }
        int InvokeCompiler(System.Collections.Generic.IEnumerable<string> args, Action<string> onStdErr, Action<string> onStdOut, bool showIncludes, System.Collections.Generic.List<string> foundIncludes);
        int InvokePreprocessor(System.IO.StreamWriter stdout);
        bool Linking { get; set; }
        string ObjectTarget { get; set; }
        int ParentPid { get; set; }
        bool PdbExistsAlready { get; set; }
        string PdbFile { get; set; }
        bool PrecompiledHeaders { get; set; }
        bool ProcessArguments(string[] args);
        string ResponseFile { get; set; }
        void SetEnvironment(System.Collections.Generic.Dictionary<string, string> envs);
        void SetWorkingDirectory(string path);
        bool SingleSource { get; }
        string SingleSourceFile { get; }
        string[] SourceFiles { get; }
        string WorkingDirectory { get; }

        Action<string> StdErrorCallback { get; set; }
        Action<string> StdOutputCallback { get; set; }

        StringBuilder StdErrorText { get; }
        StringBuilder StdOutputText { get; }

    }
}

