using CommandLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!EEHeap", HelpText = "Display general information about the CLR heaps in the process.")]
    class EEHeap : ICommand
    {
        public void Execute(CommandExecutionContext context)
        {
            context.WriteLine("GC regions:");
            context.WriteLine("{0,-20} {1,-12} {2,-12} {3,-7} {4}", "Address", "Size", "Type", "Heap#", "Commit/Reserve");
            foreach (var region in context.Runtime.EnumerateMemoryRegions().Where(r => r.Type == ClrMemoryRegionType.GCSegment || r.Type == ClrMemoryRegionType.ReservedGCSegment))
            {
                context.WriteLine("{0,-20:x16} {1,-12} {2,-12} {3,-7} {4}",
                    region.Address, region.Size.ToMemoryUnits(), region.GCSegmentType, region.HeapNumber,
                    region.Type == ClrMemoryRegionType.GCSegment ? "Committed" : "Reserved");
            }

            var heap = context.Runtime.GetHeap();
            context.WriteLine("{0,-6} {1,-12}", "Gen", "Size");
            for (int gen = 0; gen <= 3; ++gen)
            {
                context.WriteLine("{0,-6} {1,-12}",
                    gen != 3 ? gen.ToString() : "LOH", heap.GetSizeByGen(gen).ToMemoryUnits());
            }
            context.WriteLine("{0,-6} {1,-12}", "Total", heap.TotalHeapSize.ToMemoryUnits());

            context.WriteLine("Other regions:");
            context.WriteLine("{0,-20} {1,-12} {2,-7}", "Address", "Size", "Type");
            foreach (var region in context.Runtime.EnumerateMemoryRegions().Where(r => r.Type != ClrMemoryRegionType.GCSegment && r.Type != ClrMemoryRegionType.ReservedGCSegment))
            {
                context.WriteLine("{0,-20:x16} {1,-12} {2,-7}", region.Address, region.Size.ToMemoryUnits(), region.Type);
            }
        }
    }
}
