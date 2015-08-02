using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.DumpFileNoHeap, TargetType.LiveProcess)]
    [Verb("!CLRStack", HelpText="Displays the managed call stack of the current thread.")]
    class CLRStack : ICommand
    {
        [Option('a', HelpText = "Display method arguments and local variables.")]
        public bool DisplayArgumentsAndLocals { get; set; }

        private CommandExecutionContext _context;

        public void Execute(CommandExecutionContext context)
        {
            _context = context;

            var thread = context.CurrentThread;
            if (thread == null)
            {
                context.WriteError("There is no current managed thread");
                return;
            }

            thread.WriteCurrentStackTraceToContext(context, DisplayArgumentsAndLocals);
        }
    }
}
