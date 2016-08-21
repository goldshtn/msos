using CmdLine;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    enum AnalysisResult
    {
        CompletedSuccessfully,
        InternalError
    }

    class ReportDocument
    {
        public DateTime AnalysisStartTime { get; } = DateTime.Now;
        public DateTime AnalysisEndTime { get; set; }
        public AnalysisResult AnalysisResult { get; set; } = AnalysisResult.CompletedSuccessfully;
        public List<IReportComponent> Components { get; } = new List<IReportComponent>();
    }

    interface IReportComponent
    {
        string Title { get; }
        bool Generate(CommandExecutionContext context);
    }

    class DumpInformationComponent : IReportComponent
    {
        public string Title { get; private set; }
        public string DumpType { get; private set; }
        public string ExecutableName { get; private set; }
        public uint ProcessUpTimeInSeconds { get; private set; }
        public uint SystemUpTimeInSeconds { get; private set; }
        public DateTimeOffset SessionTime { get; private set; }
        public uint NumberOfProcessors { get; private set; }
        public uint WindowsBuildNumber { get; private set; }
        public string WindowsServicePack { get; private set; }
        public uint WindowsServicePackNumber { get; private set; }
        public string WindowsBuild { get; private set; }

        public bool Generate(CommandExecutionContext context)
        {
            Title = Path.GetFileName(context.DumpFile);
            switch (context.TargetType)
            {
                case TargetType.DumpFile:
                    DumpType = "Full memory dump with heap";
                    break;
                case TargetType.DumpFileNoHeap:
                    DumpType = "Mini dump with no heap";
                    break;
                default:
                    DumpType = "Unsupported dump file type";
                    break;
            }
            using (var target = context.CreateTemporaryDbgEngTarget())
            {
                IDebugSystemObjects2 sysObjects = (IDebugSystemObjects2)target.DebuggerInterface;
                IDebugControl2 control = (IDebugControl2)target.DebuggerInterface;

                uint dummy;
                StringBuilder exeName = new StringBuilder(2048);
                if (HR.Succeeded(sysObjects.GetCurrentProcessExecutableName(exeName, exeName.Capacity, out dummy)))
                    ExecutableName = exeName.ToString();

                uint uptime;
                if (HR.Succeeded(sysObjects.GetCurrentProcessUpTime(out uptime)))
                    ProcessUpTimeInSeconds = uptime;

                if (HR.Succeeded(control.GetCurrentSystemUpTime(out uptime)))
                    SystemUpTimeInSeconds = uptime;

                uint time;
                if (HR.Succeeded(control.GetCurrentTimeDate(out time)))
                    SessionTime = DateTimeOffset.FromUnixTimeSeconds(time);

                uint num;
                if (HR.Succeeded(control.GetNumberProcessors(out num)))
                    NumberOfProcessors = num;

                uint platformId, major, minor, servicePackNumber;
                StringBuilder servicePack = new StringBuilder(1048);
                StringBuilder build = new StringBuilder(1048);
                if (HR.Succeeded(control.GetSystemVersion(out platformId, out major, out minor, servicePack, servicePack.Capacity, out dummy, out servicePackNumber, build, build.Capacity, out dummy)))
                {
                    WindowsBuildNumber = minor;
                    WindowsServicePack = servicePack.ToString();
                    WindowsServicePackNumber = servicePackNumber;
                    WindowsBuild = build.ToString();
                }
            }
            return true;
        }
    }

    class RecommendationsComponent : IReportComponent
    {
        public string Title => "Issues and next steps";

        public bool Generate(CommandExecutionContext context)
        {
            return false;
        }
    }

    class UnhandledExceptionComponent : IReportComponent
    {
        public class ExceptionInfo
        {
            public string ExceptionType { get; set; }
            public string ExceptionMessage { get; set; }
            public List<string> StackFrames { get; set; }
            public ExceptionInfo InnerException { get; set; }
        }

        public string Title => "The process encountered an unhandled exception";
        public uint ExceptionCode { get; private set; }
        public ExceptionInfo Exception { get; private set; }
        public uint OSThreadId { get; private set; }
        public int ManagedThreadId { get; private set; }
        public string ThreadName { get; private set; }

        public bool Generate(CommandExecutionContext context)
        {
            using (var target = context.CreateTemporaryDbgEngTarget())
            {
                var lastEvent = target.GetLastEventInformation();
                if (lastEvent == null)
                    return false;

                var threadWithException = context.Runtime.Threads.SingleOrDefault(t => t.OSThreadId == lastEvent.OSThreadId);
                if (threadWithException == null)
                    return false;

                ExceptionCode = lastEvent.ExceptionRecord?.ExceptionCode ?? 0;
                if (ExceptionCode == 0)
                    return false;

                OSThreadId = threadWithException.OSThreadId;
                ManagedThreadId = threadWithException.ManagedThreadId;
                ThreadName = threadWithException.SpecialDescription(); // TODO Get the actual name if possible

                // Note that we might have an exception, but if it wasn't managed
                // then the Thread.CurrentException field will be null. In that case,
                // we report only the Win32 exception code.
                // TODO We could try to translate to a human-readable exception string?
                var exception = threadWithException.CurrentException;
                if (exception == null)
                    return true;

                var exceptionInfo = Exception = new ExceptionInfo();
                while (true)
                {
                    exceptionInfo.ExceptionType = exception.Type.Name;
                    exceptionInfo.ExceptionMessage = exception.Message;
                    exceptionInfo.StackFrames = exception.StackTrace.Select(f => f.DisplayString).ToList();

                    exception = exception.Inner;
                    if (exception == null)
                        break;
                    exceptionInfo.InnerException = new ExceptionInfo();
                    exceptionInfo = exceptionInfo.InnerException;
                }
            }

            return true;
        }
    }

    class LoadedModulesComponent : IReportComponent
    {
        public class LoadedModule
        {
            public string Name { get; set; }
            public ulong Size { get; set; }
            public string Path { get; set; }
            public string Version { get; set; }
            public bool IsManaged { get; set; }
        }

        public string Title { get { return "Loaded modules"; } }
        public List<LoadedModule> Modules { get; } = new List<LoadedModule>();

        public bool Generate(CommandExecutionContext context)
        {
            foreach (var module in context.Runtime.DataTarget.EnumerateModules())
            {
                var loadedModule = new LoadedModule
                {
                    Name = Path.GetFileName(module.FileName),
                    Size = module.FileSize,
                    Path = module.FileName,
                    Version = module.Version.ToString(),
                    IsManaged = module.IsManaged
                };
                Modules.Add(loadedModule);
            }
            return true;
        }
    }

    class ThreadStacksComponent : IReportComponent
    {
        public class StackFrame
        {
            public string Module { get; set; }
            public string Method { get; set; }
            public string SourceFileName { get; set; }
            public uint SourceLineNumber { get; set; }
        }

        public class ThreadInfo
        {
            [JsonIgnore]
            public uint EngineThreadId { get; set; }
            public uint OSThreadId { get; set; }
            public int ManagedThreadId { get; set; }
            public uint PriorityClass { get; set; }
            public uint Priority { get; set; }
            public ulong Affinity { get; set; }
            // The CreateTime can be correlated with system and process uptime from the dump
            // information report component.
            public ulong CreateTime { get; set; }
            public ulong KernelTime { get; set; }
            public ulong UserTime { get; set; }
            public List<StackFrame> StackFrames { get; } = new List<StackFrame>();

            public void Fill(IDebugAdvanced2 debugAdvanced)
            {
                int size = Marshal.SizeOf(typeof(DEBUG_THREAD_BASIC_INFORMATION));
                byte[] buffer = new byte[size];
                if (HR.Failed(debugAdvanced.GetSystemObjectInformation(
                    DEBUG_SYSOBJINFO.THREAD_BASIC_INFORMATION, 0, EngineThreadId, buffer, buffer.Length, out size)))
                    return;

                var gch = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    var threadBasicInformation = (DEBUG_THREAD_BASIC_INFORMATION)
                        Marshal.PtrToStructure(gch.AddrOfPinnedObject(), typeof(DEBUG_THREAD_BASIC_INFORMATION));
                    if ((threadBasicInformation.Valid & DEBUG_TBINFO.AFFINITY) != 0)
                        Affinity = threadBasicInformation.Affinity;
                    if ((threadBasicInformation.Valid & DEBUG_TBINFO.PRIORITY_CLASS) != 0)
                        PriorityClass = threadBasicInformation.PriorityClass;
                    if ((threadBasicInformation.Valid & DEBUG_TBINFO.PRIORITY) != 0)
                        Priority = threadBasicInformation.Priority;
                    if ((threadBasicInformation.Valid & DEBUG_TBINFO.TIMES) != 0)
                    {
                        CreateTime = threadBasicInformation.CreateTime;
                        KernelTime = threadBasicInformation.KernelTime;
                        UserTime = threadBasicInformation.UserTime;
                    }
                }
                finally
                {
                    gch.Free();
                }
            }
        }

        public string Title => "Thread stacks";
        public List<ThreadInfo> Threads { get; } = new List<ThreadInfo>();

        public bool Generate(CommandExecutionContext context)
        {
            using (var target = context.CreateTemporaryDbgEngTarget())
            {
                var debugAdvanced = (IDebugAdvanced2)target.DebuggerInterface;
                var stackTraces = new UnifiedStackTrace(target.DebuggerInterface, context);
                foreach (var thread in stackTraces.Threads)
                {
                    var stackTrace = stackTraces.GetStackTrace(thread.Index);
                    var threadInfo = new ThreadInfo
                    {
                        EngineThreadId = thread.EngineThreadId,
                        OSThreadId = thread.OSThreadId,
                        ManagedThreadId = thread.ManagedThread?.ManagedThreadId ?? -1
                    };
                    threadInfo.Fill(debugAdvanced);
                    foreach (var frame in stackTrace)
                    {
                        threadInfo.StackFrames.Add(new StackFrame
                        {
                            Module = frame.Module,
                            Method = frame.Method,
                            SourceFileName = frame.SourceFileName,
                            SourceLineNumber = frame.SourceLineNumber
                        });
                    }
                    Threads.Add(threadInfo);
                }
            }
            return true;
        }
    }

    class LocksAndWaitsComponent : IReportComponent
    {
        public class LockInfo
        {
            public string Reason { get; set; }
            public ulong Object { get; set; }
            public string ObjectType { get; set; }
            public List<ThreadInfo> OwnerThreads { get; } = new List<ThreadInfo>();
            public List<ThreadInfo> WaitingThreads { get; } = new List<ThreadInfo>();
        }

        public class ThreadInfo
        {
            public uint OSThreadId { get; set; }
            public int ManagedThreadId { get; set; }
            public List<LockInfo> Locks { get; } = new List<LockInfo>();
        }

        public string Title => "Locks and waits";
        public List<ThreadInfo> Threads { get; } = new List<ThreadInfo>();

        private static ThreadInfo ThreadInfoFromThread(ClrThread thread)
        {
            return new ThreadInfo
            {
                OSThreadId = thread.OSThreadId,
                ManagedThreadId = thread.ManagedThreadId
            };
        }

        public bool Generate(CommandExecutionContext context)
        {
            foreach (var thread in context.Runtime.Threads)
            {
                if (thread.BlockingObjects.Count == 0)
                    continue;

                var ti = ThreadInfoFromThread(thread);
                foreach (var blockingObject in thread.BlockingObjects)
                {
                    var li = new LockInfo
                    {
                        Reason = blockingObject.Reason.ToString(),
                        Object = blockingObject.Object,
                        ObjectType = context.Heap.GetObjectType(blockingObject.Object)?.Name
                    };
                    li.OwnerThreads.AddRange(blockingObject.Owners.Where(o => o != null).Select(ThreadInfoFromThread));
                    li.WaitingThreads.AddRange(blockingObject.Waiters.Select(ThreadInfoFromThread));
                    ti.Locks.Add(li);
                }
                Threads.Add(ti);
            }
            return Threads.Any();
        }
    }

    class MemoryUsageComponent : IReportComponent
    {
        public string Title => "Memory usage";
        public ProcessorArchitecture Architecture =>
            Environment.Is64BitProcess ? ProcessorArchitecture.Amd64 : ProcessorArchitecture.X86;
        public ulong AddressSpaceSize { get; private set; }
        public ulong VirtualSize { get; private set; }
        public ulong FreeSize { get; private set; }
        public ulong LargestFreeBlockSize { get; private set; }
        public ulong CommitSize { get; private set; }
        public ulong WorkingSetSize { get; private set; }
        public ulong PrivateSize { get; private set; }
        public ulong ManagedHeapSize { get; private set; }
        public ulong ManagedHeapCommittedSize { get; private set; }
        public ulong ManagedHeapReservedSize { get; private set; }
        public ulong Generation0Size { get; private set; }
        public ulong Generation1Size { get; private set; }
        public ulong Generation2Size { get; private set; }
        public ulong LargeObjectHeapSize { get; private set; }
        public ulong StacksSize { get; private set; }
        public ulong Win32HeapSize { get; private set; }
        public ulong ModulesSize { get; private set; }

        public bool Generate(CommandExecutionContext context)
        {
            if (context.TargetType == TargetType.DumpFileNoHeap)
                return false;

            using (var target = context.CreateTemporaryDbgEngTarget())
            {
                var vmRegions = target.EnumerateVMRegions().ToList();
                AddressSpaceSize = vmRegions.Last().BaseAddress + vmRegions.Last().RegionSize;
                VirtualSize = (ulong)vmRegions
                    .Where(r => (r.State & Microsoft.Diagnostics.Runtime.Interop.MEM.FREE) == 0)
                    .Sum(r => (long)r.RegionSize);
                FreeSize = AddressSpaceSize - VirtualSize;
                LargestFreeBlockSize = vmRegions
                    .Where(r => (r.State & Microsoft.Diagnostics.Runtime.Interop.MEM.FREE) != 0)
                    .Max(r => r.RegionSize);
                CommitSize = (ulong)vmRegions
                    .Where(r => (r.State & Microsoft.Diagnostics.Runtime.Interop.MEM.COMMIT) != 0)
                    .Sum(r => (long)r.RegionSize);
                PrivateSize = (ulong)vmRegions
                    .Where(r => (r.Type & Microsoft.Diagnostics.Runtime.Interop.MEM.PRIVATE) != 0)
                    .Sum(r => (long)r.RegionSize);
                ManagedHeapSize = context.Heap.TotalHeapSize;
                ManagedHeapCommittedSize = (ulong)context.Heap.Segments.Sum(s => (long)(s.CommittedEnd - s.Start));
                ManagedHeapReservedSize = (ulong)context.Heap.Segments.Sum(s => (long)(s.ReservedEnd - s.Start));
                Generation0Size = context.Heap.GetSizeByGen(0);
                Generation1Size = context.Heap.GetSizeByGen(1);
                Generation2Size = context.Heap.GetSizeByGen(2);
                LargeObjectHeapSize = context.Heap.GetSizeByGen(3);
                StacksSize = GetStacksSize(target);
                Win32HeapSize = GetWin32HeapSize(target);
                ModulesSize = (ulong)target.EnumerateModules().Sum(m => m.FileSize);
            }

            return true;
        }

        private ulong GetWin32HeapSize(DataTarget target)
        {
            // TODO Find the ProcessHeaps pointer in the PEB, and the NumberOfHeaps field.
            //      This is an array of _HEAP structure pointers. Each _HEAP structure has
            //      a field called Counters of type HEAP_COUNTERS (is that so on older OS
            //      versions as well?), which has information about the reserve and commit
            //      size of that heap. This isn't accurate to the level of busy/free blocks,
            //      but should be a reasonable estimate of which part of memory is used for
            //      the Win32 heap.
            //      To find the PEB, use IDebugSystemObjects::GetCurrentProcessPeb().
            return 0;
        }

        private ulong GetStacksSize(DataTarget target)
        {
            // TODO Find all the TEBs and then sum StackBase - StackLimit for all of them
            //      This is just the committed size. To get the reserved size, need
            //      to enumerate adjacent memory regions? Also, what of WoW64 threads, which
            //      really have two thread stacks? Ignore the x64 stack because it doesn't
            //      live in the 4GB address space anyway?
            //      To find the TEB for all threads, use IDebugSystemObjects::GetCurrentThreadTeb()
            //      for each of the threads (calling SetCurrentThreadId() every time).
            return 0;
        }
    }

    class TopMemoryConsumersComponent : IReportComponent
    {
        public string Title => "Top .NET memory consumers";

        public List<HeapTypeStatistics> TopConsumers { get; } = new List<HeapTypeStatistics>();

        public bool Generate(CommandExecutionContext context)
        {
            if (context.TargetType == TargetType.DumpFileNoHeap)
                return false;

            var heap = context.Heap;
            var allObjects = heap.EnumerateObjectAddresses();
            TopConsumers.AddRange(heap.GroupTypesInObjectSetAndSortBySize(allObjects).Take(100));

            return true;
        }
    }

    class MemoryFragmentationComponent : IReportComponent
    {
        public string Title => "Memory fragmentation";

        public bool Generate(CommandExecutionContext context)
        {
            return true;
        }
    }

    class FinalizationComponent : IReportComponent
    {
        public string Title => "Finalization statistics";

        public List<HeapTypeStatistics> ObjectsWaitingForFinalization { get; } = new List<HeapTypeStatistics>();
        public List<HeapTypeStatistics> ObjectsWithFinalizers { get; } = new List<HeapTypeStatistics>();
        public uint FinalizerThreadOSID { get; private set; }
        public ulong MemoryBytesReachableFromFinalizationQueue { get; private set; }

        // The finalizer thread stack can be obtained from the threads stack report.

        // Whether the finalizer thread is currently blocked can be obtained from the
        // locks and waits report, including what it is blocked on.

        // If there is very high CPU usage on this thread (obtained from the thread stacks component),
        // display a warning that indicates the finalizer thread has been excessively busy.

        public bool Generate(CommandExecutionContext context)
        {
            if (context.TargetType == TargetType.DumpFileNoHeap)
                return false;

            FinalizerThreadOSID = context.Runtime.Threads.SingleOrDefault(t => t.IsFinalizer)?.OSThreadId ?? 0;

            var readyForFinalization = context.Runtime.EnumerateFinalizerQueueObjectAddresses().ToList();
            MemoryBytesReachableFromFinalizationQueue = context.Heap.SizeReachableFromObjectSet(readyForFinalization);
            ObjectsWaitingForFinalization.AddRange(
                context.Heap.GroupTypesInObjectSetAndSortBySize(readyForFinalization));
            ObjectsWithFinalizers.AddRange(context.Heap.GroupTypesInObjectSetAndSortBySize(
                context.Heap.EnumerateFinalizableObjectAddresses()));

            return true;
        }
    }

    [Verb("report", HelpText = "Generate an automatic analysis report of the dump file with recommendations in a JSON format.")]
    [SupportedTargets(TargetType.DumpFile, TargetType.DumpFileNoHeap)]
    class Report : ICommand
    {
        [Option('f', Required = true, HelpText = "The name of the report file.")]
        public string FileName { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            var reportDocument = new ReportDocument();

            var components = from type in Assembly.GetExecutingAssembly().GetTypes()
                             where type.GetInterface(typeof(IReportComponent).FullName) != null
                             select (IReportComponent)Activator.CreateInstance(type);

            foreach (var component in components)
            {
                if (component.Generate(context))
                    reportDocument.Components.Add(component);

                // TODO Handle errors
            }

            reportDocument.AnalysisEndTime = DateTime.Now;

            string jsonReport = JsonConvert.SerializeObject(reportDocument, Formatting.Indented, new StringEnumConverter());
            File.WriteAllText(FileName, jsonReport);
        }
    }
}
