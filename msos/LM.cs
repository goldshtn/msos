using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!lm", HelpText = "Displays the loaded modules (managed only).")]
    class LM : ICommand
    {
        public void Execute(CommandExecutionContext context)
        {
            context.WriteLine("{0,-20:x16} {1,-10:x16} {2,-10} {3}", "start", "size", "symloaded", "filename");
            foreach (var module in context.Runtime.EnumerateModules())
            {
                context.WriteLine("{0,-20:x16} {1,-10:x8} {2,-10} {3}",
                    module.ImageBase, module.Size, module.IsPdbLoaded, module.FileName);
            }
        }
    }
}
