using CommandLine;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!memstats", HelpText =
        "Display memory-related statistics and fragmentation information on the CLR heaps and virtual address space.")]
    class MemStats : ICommand
    {
        [Option("vm", HelpText = "Display virtual memory ranges and what they are used for.")]
        public bool DisplayVirtualMemoryRanges { get; set; }

        [Option("vmstat", HelpText = "Display virtual memory statistics and fragmentation information.")]
        public bool DisplayVirtualMemoryStatistics { get; set; }

        [Option("heap", HelpText = "Display CLR heap information.")]
        public bool DisplayManagedHeapStatistics { get; set; }

        [Option("heapfrag", HelpText = "Display CLR heap fragmentation information.")]
        public bool DisplayManagedHeapFragmentation { get; set; }

        private CommandExecutionContext _context;

        public void Execute(CommandExecutionContext context)
        {
            _context = context;

            if (DisplayVirtualMemoryStatistics)
                VirtualMemoryStatistics();

            if (DisplayVirtualMemoryRanges)
                VirtualMemoryRanges();

            if (DisplayManagedHeapStatistics)
                ManagedHeapStatistics();

            if (DisplayManagedHeapFragmentation)
                ManagedHeapFragmentation();
        }

        private IEnumerable<MEMORY_BASIC_INFORMATION64> EnumerateVMRegions()
        {
            using (var target = _context.CreateTemporaryDbgEngTarget())
            {
                var dataSpaces = (IDebugDataSpaces4)target.DebuggerInterface;
                ulong maxAddress = Environment.Is64BitProcess ? uint.MaxValue : ulong.MaxValue;
                for (ulong address = 0; address < maxAddress; )
                {
                    MEMORY_BASIC_INFORMATION64 memInfo;
                    if (0 != dataSpaces.QueryVirtual(address, out memInfo))
                        break;

                    if (memInfo.RegionSize == 0)
                        break;

                    yield return memInfo;

                    address += memInfo.RegionSize;
                }
            }
        }

        private void VirtualMemoryStatistics()
        {
            var sizeByType = new Dictionary<MEM, ulong>();
            var sizeByState = new Dictionary<MEM, ulong>();
            ulong largestFreeRegionSize = 0;
            foreach (var region in EnumerateVMRegions())
            {
                if (region.Type != 0)
                {
                    if (!sizeByType.ContainsKey(region.Type))
                        sizeByType.Add(region.Type, 0);

                    sizeByType[region.Type] += region.RegionSize;
                }

                if (!sizeByState.ContainsKey(region.State))
                    sizeByState.Add(region.State, 0);

                sizeByState[region.State] += region.RegionSize;

                if (region.State == MEM.FREE)
                {
                    largestFreeRegionSize = Math.Max(largestFreeRegionSize, region.RegionSize);
                }
            }

            _context.WriteLine("Virtual memory statistics:");
            _context.WriteLine();
            foreach (var kvp in sizeByType)
            {
                _context.WriteLine("{0,-10} {1,-16}", kvp.Key, kvp.Value.ToMemoryUnits());
            }
            _context.WriteLine();
            foreach (var kvp in sizeByState)
            {
                _context.WriteLine("{0,-10} {1,-16}", kvp.Key, kvp.Value.ToMemoryUnits());
            }
            _context.WriteLine();
            _context.WriteLine("Largest free region size: {0}", largestFreeRegionSize.ToMemoryUnits());
            _context.WriteLine();
        }

        private void VirtualMemoryRanges()
        {
            _context.WriteLine("Virtual address ranges:");
            _context.WriteLine(
                "{0,-20} {1,-20} {2,-12} {3,-20} {4,-12}",
                "BaseAddr", "Size", "State", "Protect", "Type");

            foreach (var region in EnumerateVMRegions())
            {
                _context.WriteLine(
                    "{0,-20:x16} {1,-20:x16} {2,-12} {3,-20} {4,-12}",
                    region.BaseAddress, region.RegionSize,
                    region.State, region.Protect, region.Type);
            }
            _context.WriteLine();
        }

        private void ManagedHeapFragmentation()
        {
            var freeSpaceBySegment = new Dictionary<ClrSegment, ulong>();
            ulong totalFreeSize = 0;
            foreach (ClrSegment segment in _context.Heap.Segments)
            {
                for (ulong currentObject = segment.FirstObject; currentObject != 0;
                    currentObject = segment.NextObject(currentObject))
                {
                    ClrType type = _context.Heap.GetObjectType(currentObject);
                    if (type != null && type.IsFree)
                    {
                        ulong size = type.GetSize(currentObject);
                        if (!freeSpaceBySegment.ContainsKey(segment))
                            freeSpaceBySegment.Add(segment, size);
                        else
                            freeSpaceBySegment[segment] += size;
                        totalFreeSize += size;
                    }
                }
            }

            _context.WriteLine("Fragmentation statistics:");
            _context.WriteLine(
                "{0,-4} {1,-20} {2,-12} {3,-12} {4,-12} {5,-12} {6,-10} {7,-10}",
                "#", "Base", "Size", "Committed", "Reserved", "Fragmented", "% Frag", "Type");
            for (int segmentIdx = 0; segmentIdx < _context.Heap.Segments.Count; ++segmentIdx)
            {
                var segment = _context.Heap.Segments[segmentIdx];
                var fragmented = freeSpaceBySegment.ContainsKey(segment) ? freeSpaceBySegment[segment] : 0;
                _context.Write(
                    "{0,-4} {1,-20:x16} {2,-12} {3,-12} {4,-12} {5,-12} {6,-10:0.00%} {7,-5} ",
                    segmentIdx,
                    segment.Start,
                    segment.Length.ToMemoryUnits(),
                    (segment.CommittedEnd - segment.Start).ToMemoryUnits(),
                    (segment.ReservedEnd - segment.Start).ToMemoryUnits(),
                    fragmented.ToMemoryUnits(),
                    fragmented / (double)segment.Length,
                    segment.IsLarge ? "LOH" : "SOH");
                _context.WriteLink(
                    "",
                    String.Format("!hq --tabular from o in ObjectsInSegment({0}) " +
                                  "group (long)o.__Size by o.__Type into g " +
                                  "let totalSize = g.Sum() " + 
                                  "orderby totalSize ascending " +
                                  "select new {{ Type = g.Key, TotalSize = totalSize }}",
                                  segmentIdx)
                    );
            }
            _context.WriteLine();
            _context.WriteLine("Total size of free objects: {0}", totalFreeSize.ToMemoryUnits());
            _context.WriteLine();
        }

        private void ManagedHeapStatistics()
        {
            _context.WriteLine("GC regions:");
            _context.WriteLine("{0,-20} {1,-12} {2,-12} {3,-7} {4}", "Address", "Size", "Type", "Heap#", "Commit/Reserve");
            foreach (var region in _context.Runtime.EnumerateMemoryRegions().Where(r => r.Type == ClrMemoryRegionType.GCSegment || r.Type == ClrMemoryRegionType.ReservedGCSegment))
            {
                _context.WriteLine("{0,-20:x16} {1,-12} {2,-12} {3,-7} {4}",
                    region.Address, region.Size.ToMemoryUnits(), region.GCSegmentType, region.HeapNumber,
                    region.Type == ClrMemoryRegionType.GCSegment ? "Committed" : "Reserved");
            }

            var heap = _context.Runtime.GetHeap();
            _context.WriteLine();
            _context.WriteLine("{0,-6} {1,-12}", "Gen", "Size");
            for (int gen = 0; gen <= 3; ++gen)
            {
                _context.WriteLine("{0,-6} {1,-12}",
                    gen != 3 ? gen.ToString() : "LOH", heap.GetSizeByGen(gen).ToMemoryUnits());
            }
            _context.WriteLine("{0,-6} {1,-12}", "Total", heap.TotalHeapSize.ToMemoryUnits());

            _context.WriteLine();
            _context.WriteLine("Other regions:");
            _context.WriteLine("{0,-20} {1,-12} {2,-7}", "Address", "Size", "Type");
            foreach (var region in _context.Runtime.EnumerateMemoryRegions().Where(r => r.Type != ClrMemoryRegionType.GCSegment && r.Type != ClrMemoryRegionType.ReservedGCSegment))
            {
                _context.WriteLine("{0,-20:x16} {1,-12} {2,-7}", region.Address, region.Size.ToMemoryUnits(), region.Type);
            }
            _context.WriteLine();
        }
    }
}
