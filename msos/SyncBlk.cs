using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.LiveProcess)]
    [Verb("!SyncBlk", HelpText = "Display information on blocking objects, their owners, and their waiters.")]
    class SyncBlk : ICommand
    {
        public void Execute(CommandExecutionContext context)
        {
            context.WriteLine("{0,-20} {1,-10} {2,-8} {3,-20} {4,-20}", "Address", "Type", "Locked", "Owner(s)", "Waiter(s)");
            foreach (var blockingObject in context.Runtime.GetHeap().EnumerateBlockingObjects())
            {
                context.Write("{0,-20:x16} {1,-10} {2,-8} {3,-20} {4,-20}",
                    blockingObject.Object, blockingObject.Reason, blockingObject.Taken ? 1 : 0,
                    String.Join(", ", from thread in blockingObject.Owners where thread != null select thread.ManagedThreadId),
                    String.Join(", ", from thread in blockingObject.Waiters where thread != null select thread.ManagedThreadId));
                context.WriteLink("", String.Format("!do {0:x16}", blockingObject.Object));
                context.WriteLine();
            }
        }
    }
}
