using CmdLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    class CommandLineOptions
    {
        [Option('z', MutuallyExclusiveSet = "target", HelpText = "The dump file to open.")]
        public string DumpFile { get; set; }

        [Option("pn", MutuallyExclusiveSet = "target", HelpText = "The process name to attach to. Fails if there are multiple processes with that name.")]
        public string ProcessName { get; set; }

        [Option("pid", MutuallyExclusiveSet = "target", HelpText = "The process id to attach to.")]
        public int ProcessId { get; set; }

        [Option("triage", MutuallyExclusiveSet = "target", HelpText = "The path pattern for dump files to automatically load, triage, and report statistics for. Example pattern: C:\\Dumps\\MyApp*.dmp")]
        public string TriagePattern { get; set; }

        [Option("diag", HelpText = "Display diagnostic information after executing each command.")]
        public bool DisplayDiagnosticInformation { get; set; }

        [Option('c', MutuallyExclusiveSet = "initial", HelpText = "The command to execute immediately after connecting to the target. Separate multiple commands by semicolons.")]
        public string InitialCommand { get; set; }

        [Option('o', HelpText = "Forward all output to the specified file instead of the console. Not implemented.")]
        public string OutputFileName { get; set; }

        [Option('i', MutuallyExclusiveSet = "initial", HelpText = "Read commands from the specified file and execute them immediately after connecting to the target.")]
        public string InputFileName { get; set; }

        [Option("clrVersion", Default = 0,
            HelpText = "The ordinal number of the CLR version to interact with, if there are multiple CLRs loaded into the target.")]
        public int ClrVersion { get; set; }
    }
}
