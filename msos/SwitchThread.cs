using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.DumpFileNoHeap, TargetType.LiveProcess)]
    [Verb("~", HelpText = "Switch to the thread that has the specified managed thread ID.")]
    class SwitchThread : ICommand
    {
        [Value(0, Required = true)]
        public int ManagedThreadId { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            if (context.Runtime.Threads.Any(t => t.ManagedThreadId == ManagedThreadId))
            {
                context.CurrentManagedThreadId = ManagedThreadId;
            }
            else
            {
                context.WriteError("No thread has the managed thread id {0}", ManagedThreadId);
            }
        }
    }
}
