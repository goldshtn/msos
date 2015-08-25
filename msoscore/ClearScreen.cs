using CmdLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.DumpFileNoHeap, TargetType.LiveProcess)]
    [Verb(".cls", HelpText = "Clears the screen. Has no effect if output is redirected to a file.")]
    class ClearScreen : ICommand
    {
        public void Execute(CommandExecutionContext context)
        {
            context.Printer.ClearScreen();
        }
    }
}
