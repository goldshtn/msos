using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("q", HelpText = "Quit the debugger.")]
    class Quit : ICommand
    {
        public void Execute(CommandExecutionContext context)
        {
            context.ShouldQuit = true;
        }
    }
}
