using CmdLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.LiveProcess)]
    [Verb("!frq", HelpText = "Display objects ready for finalization.")]
    class FReachableQueue : ICommand
    {
        [Option("stat", HelpText = "Display only statistics and not specific objects.")]
        public bool StatisticsOnly { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            var heap = context.Runtime.GetHeap();
            var readyForFinalization = context.Runtime.EnumerateFinalizerQueueObjectAddresses().ToList();
            ulong totalCount = 0;
            if (StatisticsOnly)
            {
                context.WriteLine("{0,-10} {1,-10} {2}", "Count", "Size", "Class Name");
                var query = from obj in readyForFinalization
                            let type = heap.GetObjectType(obj)
                            where type != null && !String.IsNullOrEmpty(type.Name)
                            let size = (long)type.GetSize(obj)
                            group size by type.Name into g
                            let totalSize = g.Sum()
                            let count = g.Count()
                            orderby totalSize descending
                            select new { Count = count, Size = totalSize, ClassName = g.Key };
                foreach (var row in query)
                {
                    context.WriteLine("{0,-10} {1,-10} {2}", row.Count, row.Size, row.ClassName);
                    totalCount += (ulong)row.Count;
                }
            }
            else
            {
                context.WriteLine("{0,-20} {1,-10} {2}", "Address", "Size", "Class Name");
                foreach (var objPtr in readyForFinalization)
                {
                    var type = heap.GetObjectType(objPtr);
                    if (type == null || String.IsNullOrEmpty(type.Name))
                        return;

                    context.WriteLink(
                        String.Format("{0,-20:x16} {1,-10} {2}", objPtr, type.GetSize(objPtr), type.Name),
                        String.Format("!do {0:x16}", objPtr)
                        );
                    context.WriteLine();
                    ++totalCount;
                }
            }
            context.WriteLine("# of objects ready for finalization: {0}", totalCount);
            context.WriteLine("Memory reachable from these objects: {0}",
                heap.SizeReachableFromObjectSet(readyForFinalization).ToMemoryUnits());

            var finalizerThread = context.Runtime.Threads.SingleOrDefault(t => t.IsFinalizer);
            if (finalizerThread != null && finalizerThread.BlockingObjects.Count > 0)
            {
                context.WriteLine();
                context.WriteLine("The finalizer thread is blocked! Blocking objects:");
                foreach (var blockingObject in finalizerThread.BlockingObjects)
                {
                    context.Write("\t{0} ", blockingObject.Reason);
                    string objHex = String.Format("{0:x16}", blockingObject.Object);
                    context.WriteLink(objHex, "!do " + objHex);
                    context.WriteLine();
                }
                context.WriteLine("Blocked at:");
                foreach (var frame in finalizerThread.StackTrace.Take(3))
                {
                    context.WriteLine("\t" + frame.DisplayString);
                }
                context.WriteLink(
                    "\t... view full stack",
                    String.Format("~ {0}; !clrstack", finalizerThread.ManagedThreadId));
                context.WriteLine();
            }
        }
    }
}
