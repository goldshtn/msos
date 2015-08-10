using CmdLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.DumpFileNoHeap, TargetType.LiveProcess)]
    [Verb("q", HelpText = "Quit the debugger, or leave DbgEng native mode.")]
    class Quit : ICommand
    {
        public void Execute(CommandExecutionContext context)
        {
            if (context.IsInDbgEngNativeMode)
            {
                context.ExitDbgEngNativeMode();
                context.WriteLine("Exited DbgEng mode; you are now back in msos.");
            }
            else
            {
                context.ShouldQuit = true;
            }
        }
    }
}
