using CmdLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.LiveProcess)]
    [Verb("!waits", HelpText = "Displays wait chain information and detects deadlocks.")]
    class WaitChains : ICommand
    {
        [Option("thread", HelpText =
            "Display only the wait chain of the thread with the specified managed thread id.")]
        public int SpecificThreadId { get; set; }

        private CommandExecutionContext _context;

        public void Execute(CommandExecutionContext context)
        {
            _context = context;
            if (SpecificThreadId != 0)
            {
                var thread = _context.Runtime.Threads.SingleOrDefault(t => t.ManagedThreadId == SpecificThreadId);
                if (thread == null)
                {
                    _context.WriteError("There is no managed thread with the id '{0}'.", SpecificThreadId);
                    return;
                }
                DisplayChainForThread(thread);
            }
            else
            {
                _context.Runtime.Threads.ForEach(DisplayChainForThread);
            }
        }

        private void DisplayChainForThread(ClrThread thread)
        {
            DisplayChainForThreadAux(thread, 0, new HashSet<int>());
        }

        private void DisplayChainForThreadAux(ClrThread thread, int depth, HashSet<int> visitedThreadIds)
        {
            _context.WriteLink(
                String.Format("{0}+ Thread {1}", new string(' ', depth*2), thread.ManagedThreadId),
                String.Format("~ {0}; !clrstack", thread.ManagedThreadId));
            _context.WriteLine();
            
            if (visitedThreadIds.Contains(thread.ManagedThreadId))
            {
                _context.WriteLine("{0}*** DEADLOCK!", new string(' ', depth * 2));
                return;
            }
            visitedThreadIds.Add(thread.ManagedThreadId);

            foreach (var blockingObject in thread.BlockingObjects)
            {
                _context.Write("{0}| {1} ", new string(' ', (depth+1) * 2), blockingObject.Reason);
                var type = _context.Heap.GetObjectType(blockingObject.Object);
                if (type != null && !String.IsNullOrEmpty(type.Name))
                {
                    _context.WriteLink(
                        String.Format("{0:x16} {1}", blockingObject.Object, type.Name),
                        String.Format("!do {0:x16}", blockingObject.Object));
                }
                else
                {
                    _context.Write("{0:x16}", blockingObject.Object);
                }
                _context.WriteLine();
                foreach (var owner in blockingObject.Owners)
                {
                    if (owner == null) // ClrMD sometimes reports this nonsense
                        continue;

                    DisplayChainForThreadAux(owner, depth + 2, visitedThreadIds);
                }
            }
        }
    }
}
