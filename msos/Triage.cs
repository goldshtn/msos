using CmdLine;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("triage", HelpText = "Performs basic triage of the dump file and displays results in a condensed form.")]
    [SupportedTargets(TargetType.DumpFile, TargetType.DumpFileNoHeap)]
    class Triage : ICommand
    {
        private CommandExecutionContext _context;
        private DataTarget _dbgEngTarget;

        public void Execute(CommandExecutionContext context)
        {
            // TODO: Create a command-line option too, for running this on multiple dumps and getting a summary table

            _context = context;
            using (_dbgEngTarget = context.CreateTemporaryDbgEngTarget())
            {
                GetModuleInformation();
                GetMemoryUsageInformation();
                GetFaultingThreadAndModuleInformation();
            }
        }

        private void GetFaultingThreadAndModuleInformation()
        {
            UnifiedStackTrace stackTrace = new UnifiedStackTrace(_dbgEngTarget.DebuggerInterface, _context);
            int totalThreads = (int)stackTrace.NumThreads;
            int managedThreads = stackTrace.Threads.Count(t => t.IsManagedThread);
            _context.WriteLine("THREADS  {0} total threads, {1} managed threads", totalThreads, managedThreads);

            LastEventInformation lastEventInformation = _dbgEngTarget.GetLastEventInformation();
            if (lastEventInformation == null)
                return;

            ThreadInfo faultingThread = stackTrace.Threads.SingleOrDefault(t => t.OSThreadId == lastEventInformation.OSThreadId);
            if (faultingThread == null)
                return;

            _context.WriteLine(
                "EVENT    Last event in thread OSID = {0}, managed = {1}",
                faultingThread.OSThreadId, faultingThread.IsManagedThread);
            _context.WriteLine("Event {0} - {1}", lastEventInformation.EventType, lastEventInformation.EventDescription);
            if (lastEventInformation.ExceptionRecord.HasValue)
            {
                _context.WriteLine(
                    "EVENT    Exception {0:X8} at {1:x16}",
                    lastEventInformation.ExceptionRecord.Value.ExceptionCode,
                    lastEventInformation.ExceptionRecord.Value.ExceptionAddress);
            }
            if (faultingThread.IsManagedThread && faultingThread.ManagedThread.CurrentException != null)
            {
                _context.WriteLine(
                    "EVENT    Managed exception {0} - {1}",
                    faultingThread.ManagedThread.CurrentException.Type.Name,
                    faultingThread.ManagedThread.CurrentException.Message);
            }

            _context.WriteLine("EVENT    Faulting call stack:");
            stackTrace.PrintStackTrace(_context, faultingThread.Index);
        }

        private void GetMemoryUsageInformation()
        {
            var vmRegions = _dbgEngTarget.EnumerateVMRegions().ToList();
            ulong totalCommittedBytes = (ulong)vmRegions.Sum(r => r.State == MEM.COMMIT ? (long)r.RegionSize : 0L);
            ulong totalReservedBytes = (ulong)vmRegions.Sum(r => r.State == MEM.RESERVE ? (long)r.RegionSize : 0L);
            ulong totalGCBytes = _context.Heap.TotalHeapSize;
            _context.WriteLine(
                "MEMORY   {0} committed, {1} reserved, {2} GC heap",
                totalCommittedBytes.ToMemoryUnits(),
                totalReservedBytes.ToMemoryUnits(),
                totalGCBytes.ToMemoryUnits());
        }

        private void GetModuleInformation()
        {
            int modules = _dbgEngTarget.EnumerateModules().Count();
            _context.WriteLine("MODULES   {0} modules loaded", modules);
        }
    }
}
