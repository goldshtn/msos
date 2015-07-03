using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!frq", HelpText = "Display objects ready for finalization.")]
    class FReachableQueue : ICommand
    {
        public void Execute(CommandExecutionContext context)
        {
            var heap = context.Runtime.GetHeap();
            ulong count = 0;
            context.WriteLine("{0,-20} {1,-10} {2}", "Address", "Size", "Class Name");
            foreach (var objPtr in context.Runtime.EnumerateFinalizerQueue())
            {
                var type = heap.GetObjectType(objPtr);
                if (type == null || String.IsNullOrEmpty(type.Name))
                    return;

                context.WriteLine("{0,-20:x16} {1,-10} {2}", objPtr, type.GetSize(objPtr), type.Name);
                ++count;
            }
            context.WriteLine("Total {0} objects ready for finalization", count);
        }
    }
}
