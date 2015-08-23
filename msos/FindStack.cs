using CmdLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.DumpFileNoHeap, TargetType.LiveProcess)]
    [Verb("!findstack", HelpText =
        "Finds and displays thread stacks that include the specified method, module, or parameter type.")]
    class FindStack : ICommand
    {
        [Value(0, Required = true, HelpText = "The search string.")]
        public string SearchString { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            _searchRegex = new Regex(SearchString);
            foreach (var thread in context.Runtime.Threads)
            {
                if (ThreadMatchesFilter(thread))
                {
                    context.WriteLink(
                        String.Format("Thread {0}", thread.ManagedThreadId),
                        String.Format("~ {0}", thread.ManagedThreadId)
                        );
                    context.WriteLine();
                    thread.WriteCurrentStackTraceToContext(context, displayArgumentsAndLocals: false);
                }
            }
        }

        private Regex _searchRegex;

        private bool ThreadMatchesFilter(ClrThread thread)
        {
            return thread.StackTrace.Any(frame => _searchRegex.IsMatch(frame.DisplayString));
        }
    }
}
