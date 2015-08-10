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
                var control = (IDebugControl)target.DebuggerInterface;
                DEBUG_EVENT eventType;
                uint procId, threadId;
                StringBuilder description = new StringBuilder(2048);
                uint unused;
                uint descriptionSize;
                if (0 != control.GetLastEventInformation(
                    out eventType, out procId, out threadId,
                    IntPtr.Zero, 0, out unused,
                    description, description.Capacity, out descriptionSize))
                {
                    context.WriteLine("No last event information available.");
                    return;
                }

                var osThreadIds = target.GetOSThreadIds();
                context.Write("Thread OSID = {0} ", osThreadIds[threadId]);
                var managedThread = context.Runtime.Threads.SingleOrDefault(t => t.OSThreadId == osThreadIds[threadId]);
                if (managedThread != null)
                {
                    context.WriteLine("(managed id = {0})", managedThread.ManagedThreadId);
                }
                else
                {
                    context.WriteLine("(unmanaged)");
                }
                context.WriteLine("{0} - {1}", eventType, description.ToString());
            }
        }
    }
}
