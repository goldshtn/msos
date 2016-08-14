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
    class TriageInformation
    {
        public int ModuleCount { get; set; }
        public ulong CommittedMemoryBytes { get; set; }
        public ulong ReservedMemoryBytes { get; set; }
        public ulong GCHeapMemoryBytes { get; set; }
        public int ManagedThreadCount { get; set; }
        public int TotalThreadCount { get; set; }        
        public uint FaultingThreadOSID { get; set; }        
        public bool IsFaultingThreadManaged { get; set; }
        public string EventDescription { get; set; }
        public uint ExceptionCode { get; set; }
        public string ManagedExceptionType { get; set; }
        public string FaultingModule { get; set; }
        public string FaultingMethod { get; set; }

        public string GetEventDisplayString()
        {
            return ManagedExceptionType ?? EventDescription.Replace(" (first/second chance not available)", "");
        }
    }

    [Verb("triage", HelpText = "Performs basic triage of the dump file and displays results in a condensed form.")]
    [SupportedTargets(TargetType.DumpFile, TargetType.DumpFileNoHeap)]
    class Triage : ICommand
    {
        private static readonly HashSet<string> WellKnownMicrosoftModules = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
        {
            /* Windows */ "ntdll", "kernelbase", "advapi32", "gdi32", "kernel32",
            /* CLR     */ "clr", "mscorlib", "clrjit", "protojit", "mscorwks", "mscorsvr",
            /* BCL     */ "System", "System.Core"
            // TODO Add more
        };

        private DataTarget _dbgEngTarget;
        private TriageInformation _triageInformation = new TriageInformation();

        [Option('s', Required = false, HelpText = "Show the faulting call stack if available.")]
        public bool ShowFaultingStack { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            TriageInformation triageInformation = GetTriageInformation(context);
            PrintTriageInformation(context, triageInformation);
        }

        private void PrintTriageInformation(CommandExecutionContext context, TriageInformation triageInformation)
        {
            context.WriteLine("MODULES  {0} modules loaded", triageInformation.ModuleCount);
            context.WriteLine("THREADS  {0} total threads, {1} managed threads", triageInformation.TotalThreadCount, triageInformation.ManagedThreadCount);
            context.WriteLine("MEMORY   {0} committed, {1} reserved, {2} GC heap",
                triageInformation.CommittedMemoryBytes.ToMemoryUnits(),
                triageInformation.ReservedMemoryBytes.ToMemoryUnits(),
                triageInformation.GCHeapMemoryBytes.ToMemoryUnits());

            context.WriteLine("EVENT    Last event in thread OSID = {0}, managed = {1}",
                _triageInformation.FaultingThreadOSID, _triageInformation.IsFaultingThreadManaged);
            context.WriteLine("EVENT    {0}", _triageInformation.EventDescription);
            if (_triageInformation.ExceptionCode != 0)
                context.WriteLine("EVENT    Exception {0:X8}", _triageInformation.ExceptionCode);
            if (!String.IsNullOrEmpty(_triageInformation.ManagedExceptionType))
                context.WriteLine("EVENT    Managed exception {0}", _triageInformation.ManagedExceptionType);
            context.WriteLine("EVENT    Faulting module {0}, method {1}", _triageInformation.FaultingModule, _triageInformation.FaultingMethod);
        }

        public TriageInformation GetTriageInformation(CommandExecutionContext context)
        {
            using (_dbgEngTarget = context.CreateTemporaryDbgEngTarget())
            {
                FillModuleInformation();
                FillMemoryUsageInformation(context);
                FillFaultingThreadAndModuleInformation(context);
            }
            return _triageInformation;
        }

        private void FillFaultingThreadAndModuleInformation(CommandExecutionContext context)
        {
            UnifiedStackTrace stackTrace = new UnifiedStackTrace(_dbgEngTarget.DebuggerInterface, context);
            _triageInformation.TotalThreadCount = (int)stackTrace.NumThreads;
            _triageInformation.ManagedThreadCount = stackTrace.Threads.Count(t => t.IsManagedThread);

            LastEventInformation lastEventInformation = _dbgEngTarget.GetLastEventInformation();
            if (lastEventInformation == null)
                return;

            ThreadInfo faultingThread = stackTrace.Threads.SingleOrDefault(t => t.OSThreadId == lastEventInformation.OSThreadId);
            if (faultingThread == null)
                return;

            _triageInformation.FaultingThreadOSID = faultingThread.OSThreadId;
            _triageInformation.IsFaultingThreadManaged = faultingThread.IsManagedThread;
            _triageInformation.EventDescription = lastEventInformation.EventDescription;

            if (lastEventInformation.ExceptionRecord.HasValue)
            {
                _triageInformation.ExceptionCode = lastEventInformation.ExceptionRecord.Value.ExceptionCode;
            }
            if (faultingThread.IsManagedThread && faultingThread.ManagedThread.CurrentException != null)
            {
                _triageInformation.ManagedExceptionType = faultingThread.ManagedThread.CurrentException.Type.Name;
            }

            var frames = stackTrace.GetStackTrace(faultingThread.Index);

            UnifiedStackFrame faultingFrame = frames.FirstOrDefault(f => f.Module != null && !WellKnownMicrosoftModules.Contains(f.Module));
            if (faultingFrame != null)
            {
                _triageInformation.FaultingModule = faultingFrame.Module;
                _triageInformation.FaultingMethod = faultingFrame.Method;
            }

            if (ShowFaultingStack)
            {
                context.WriteLine("Faulting call stack:");
                stackTrace.PrintStackTrace(context, frames);
            }
        }

        private void FillMemoryUsageInformation(CommandExecutionContext context)
        {
            var vmRegions = _dbgEngTarget.EnumerateVMRegions().ToList();
            _triageInformation.CommittedMemoryBytes = (ulong)vmRegions.Sum(r => r.State == MEM.COMMIT ? (long)r.RegionSize : 0L);
            _triageInformation.ReservedMemoryBytes = (ulong)vmRegions.Sum(r => r.State == MEM.RESERVE ? (long)r.RegionSize : 0L);
            _triageInformation.GCHeapMemoryBytes = context.Heap.TotalHeapSize;
        }

        private void FillModuleInformation()
        {
            _triageInformation.ModuleCount = _dbgEngTarget.EnumerateModules().Count();
        }
    }
}
