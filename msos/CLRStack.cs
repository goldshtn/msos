using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!CLRStack", HelpText="Displays the managed call stack of the current thread.")]
    class CLRStack : ICommand
    {
        // TODO Figure out how to implement these switches
        [Option('p', HelpText = "Display parameter values. Not implemented.")]
        public bool DisplayParameterValues { get; set; }

        [Option('a', HelpText = "Display parameter values and local variable values. Not implemented.")]
        public bool DisplayAllStackValues { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            var thread = context.CurrentThread;
            if (thread == null)
            {
                context.WriteError("There is no current managed thread");
                return;
            }

            thread.WriteCurrentStackTraceToContext(context);
        }
    }
}
