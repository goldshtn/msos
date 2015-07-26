using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("q", HelpText = "Quit the debugger, or leave DbgEng native mode.")]
    class Quit : ICommand
    {
        public void Execute(CommandExecutionContext context)
        {
            if (context.IsInDbgEngNativeMode)
            {
                context.ExitDbgEngNativeMode();
            }
            else
            {
                context.ShouldQuit = true;
            }
        }
    }
}
