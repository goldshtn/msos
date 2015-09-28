using CmdLine;
using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.DumpFileNoHeap, TargetType.LiveProcess)]
    [Verb(".lastevent", HelpText =
        "Displays the last event in the dump file. If there was an exception, it will be the last event.")]
    class LastEvent : ICommand
    {
        public void Execute(CommandExecutionContext context)
        {
            using (var target = context.CreateTemporaryDbgEngTarget())
            {
                LastEventInformation lastEventInformation = target.GetLastEventInformation();
                if (lastEventInformation == null)
                {
                    context.WriteLine("Last event information is not available");
                    return;
                }

                context.Write("Thread OSID = {0} ", lastEventInformation.OSThreadId);
                var managedThread = context.Runtime.Threads.SingleOrDefault(t => t.OSThreadId == lastEventInformation.OSThreadId);
                if (managedThread != null)
                {
                    context.WriteLine("(managed id = {0})", managedThread.ManagedThreadId);
                }
                else
                {
                    context.WriteLine("(unmanaged)");
                }
                context.WriteLine("{0} - {1}", lastEventInformation.EventType, lastEventInformation.EventDescription);
            }
        }
    }
}
