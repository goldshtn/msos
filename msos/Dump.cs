using CmdLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.LiveProcess)]
    [Verb(".dump", HelpText = "Generate a dump file of the target process.")]
    class Dump : ICommand
    {
        [Option('t', Default = DumpWriter.DumpType.FullMemoryExcludingSafeRegions,
            HelpText = "The type of the dump to generate.")]
        public DumpWriter.DumpType DumpType { get; set; }

        [Option('f', Required = true,
            HelpText = "The name of the resulting dump file.")]
        public string FileName { get; set; }

        [Option("verbose", HelpText =
            "Display verbose diagnostic output while capturing the dump file.")]
        public bool Verbose { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            var writer = new DumpWriter.DumpWriter(
                Verbose ? new PrinterTextWriter(context.Printer) : null
                );
            writer.Dump(context.ProcessId, DumpType, FileName /*TODO comment*/);
            context.WriteLine("Resulting dump size: {0:N0} bytes", new FileInfo(FileName).Length);
        }
    }
}
