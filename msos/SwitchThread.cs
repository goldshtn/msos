using CmdLine;
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
        [Value(0, Required = true, HelpText = "The managed thread id of the thread to switch to.")]
        public int ManagedThreadId { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            if (context.Runtime.Threads.Any(t => t.ManagedThreadId == ManagedThreadId))
            {
                context.CurrentManagedThreadId = ManagedThreadId;
            }
            else
            {
                context.WriteErrorLine("No thread has the managed thread id {0}", ManagedThreadId);
            }
        }
    }
}
