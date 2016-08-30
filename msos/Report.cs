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
        public string AnalysisError { get; set; }
        public List<IReportComponent> Components { get; } = new List<IReportComponent>();
    }

    enum ReportRecommendationSeverity
    {
        Critical,
        Warning,
        Informational
    }

    interface IReportRecommendation
    {
        string Description { get; }
        ReportRecommendationSeverity Severity { get; }
    }

    interface IReportComponent
    {
        string Title { get; }
        bool Generate(CommandExecutionContext context);
        List<IReportRecommendation> Recommendations { get; }
    }

    abstract class ReportComponent : IReportComponent
    {
        public abstract string Title { get; }

        public abstract bool Generate(CommandExecutionContext context);

        public List<IReportRecommendation> Recommendations { get; } = new List<IReportRecommendation>();
    }

    class DumpInformationComponent : ReportComponent
    {
        private string _title;

        public override string Title { get { return _title; } }
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
        public List<string> ClrVersions { get; } = new List<string>();

        public override bool Generate(CommandExecutionContext context)
        {
            _title = Path.GetFileName(context.DumpFile);
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
            ClrVersions.AddRange(context.Runtime.DataTarget.ClrVersions.Select(v => v.Version.ToString()));

            if (context.Runtime.DataTarget.ClrVersions.Any(v => v.Version.Minor == 2))
                Recommendations.Add(new CLRV2Detected());

            return true;
        }

        class CLRV2Detected : IReportRecommendation
        {
            public ReportRecommendationSeverity Severity => ReportRecommendationSeverity.Informational;

            public string Description =>
                "CLR 2.0 was detected in this process. CLR 2.0 is an extremely old version " +
                "of the CLR. Although it may still be supported by Microsoft, it is strongly " +
                "advised to migrate to a newer version of the runtime if possible.";
        }
    }

    class UnhandledExceptionComponent : ReportComponent
    {
        public class ExceptionInfo
        {
            public string ExceptionType { get; set; }
            public string ExceptionMessage { get; set; }
            public List<string> StackFrames { get; set; }
            public ExceptionInfo InnerException { get; set; }
        }

        public override string Title => "The process encountered an unhandled exception";
        public uint ExceptionCode { get; private set; }
        public ExceptionInfo Exception { get; private set; }
        public uint OSThreadId { get; private set; }
        public int ManagedThreadId { get; private set; }

        public override bool Generate(CommandExecutionContext context)
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

                // Note that we might have an exception, but if it wasn't managed
                // then the Thread.CurrentException field will be null. In that case,
                // we report only the Win32 exception code.
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

            if (Exception != null)
                Recommendations.Add(new UnhandledExceptionOccurred { Exception = Exception });

            return true;
        }

        class UnhandledExceptionOccurred : IReportRecommendation
        {
            public ExceptionInfo Exception { get; set; }

            public ReportRecommendationSeverity Severity => ReportRecommendationSeverity.Critical;

            public string Description => "An unhandled exception occurred in the target process.";
        }
    }

    class LoadedModulesComponent : ReportComponent
    {
        public class LoadedModule
        {
            public string Name { get; set; }
            public ulong Size { get; set; }
            public string Path { get; set; }
            public string Version { get; set; }
            public bool IsManaged { get; set; }
        }

        public override string Title => "Loaded modules";
        public List<LoadedModule> Modules { get; } = new List<LoadedModule>();

        public override bool Generate(CommandExecutionContext context)
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

    class ThreadStacksComponent : ReportComponent
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
            public string SpecialDescription { get; set; }
            public string ThreadName { get; set; }
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

        public override string Title => "Thread stacks";
        public List<ThreadInfo> Threads { get; } = new List<ThreadInfo>();

        private const ulong SEC_TO_100NSEC = 10000000;
        private uint _processUpTimeInSeconds;

        private Dictionary<int, string> GetManagedThreadNames(ClrHeap heap)
        {
            var result = new Dictionary<int, string>();
            if (!heap.CanWalkHeap)
                return result;

            var threadObjects = from obj in heap.EnumerateObjectAddresses()
                                let type = heap.GetObjectType(obj)
                                where type != null && type.Name == "System.Threading.Thread"
                                select obj;
            var threadType = heap.GetTypeByName("System.Threading.Thread");
            var nameField = threadType.GetFieldByName("m_Name");
            var managedIdField = threadType.GetFieldByName("m_ManagedThreadId");

            foreach (var threadObject in threadObjects)
            {
                string name = (string)nameField.GetValue(threadObject);
                int id = (int)managedIdField.GetValue(threadObject);
                result.Add(id, name);
            }

            return result;
        }

        public override bool Generate(CommandExecutionContext context)
        {
            var managedThreadNames = GetManagedThreadNames(context.Heap);

            using (var target = context.CreateTemporaryDbgEngTarget())
            {
                IDebugSystemObjects3 sysObjects = (IDebugSystemObjects3)target.DebuggerInterface;
                sysObjects.GetCurrentProcessUpTime(out _processUpTimeInSeconds);

                var debugAdvanced = (IDebugAdvanced2)target.DebuggerInterface;
                var stackTraces = new UnifiedStackTraces(target.DebuggerInterface, context);
                foreach (var thread in stackTraces.Threads)
                {
                    var stackTrace = stackTraces.GetStackTrace(thread.Index);
                    var threadInfo = new ThreadInfo
                    {
                        EngineThreadId = thread.EngineThreadId,
                        OSThreadId = thread.OSThreadId,
                        ManagedThreadId = thread.ManagedThread?.ManagedThreadId ?? -1,
                        SpecialDescription = thread.ManagedThread?.SpecialDescription()
                    };

                    string threadName;
                    if (managedThreadNames.TryGetValue(threadInfo.ManagedThreadId, out threadName))
                        threadInfo.ThreadName = threadName;

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

            RecommendFinalizerThreadHighCPU(context);
            RecommendHighNumberOfThreads();

            return true;
        }

        private void RecommendHighNumberOfThreads()
        {
            if (Threads.Count > 200)
                Recommendations.Add(new HighNumberOfThreads { Count = Threads.Count });
        }

        private void RecommendFinalizerThreadHighCPU(CommandExecutionContext context)
        {
            var finalizerThread = context.Runtime.Threads.SingleOrDefault(t => t.IsFinalizer);
            var info = Threads.SingleOrDefault(t => t.OSThreadId == finalizerThread.OSThreadId);
            ulong executionTimeIn100Ns = info.KernelTime + info.UserTime;
            ulong totalTimeIn100Ns = _processUpTimeInSeconds * SEC_TO_100NSEC;
            double executionPercent = 100.0 * executionTimeIn100Ns / totalTimeIn100Ns;
            if (executionPercent > 5.0)
                Recommendations.Add(new FinalizerThreadHighCPU { ExecutionPercent = executionPercent });
        }

        class HighNumberOfThreads : IReportRecommendation
        {
            public int Count { get; set; }

            public ReportRecommendationSeverity Severity => ReportRecommendationSeverity.Warning;

            public string Description =>
                "The process has a relatively high number of threads. This can be an indicator of " +
                "inefficient use of the thread pool and the TPL. Threads can also be responsible for " +
                "high address space consumption -- each thread stack can consume up to 1MB of memory.";
        }

        class FinalizerThreadHighCPU : IReportRecommendation
        {
            public double ExecutionPercent { get; set; }

            public ReportRecommendationSeverity Severity
            {
                get
                {
                    if (ExecutionPercent < 10.0)
                        return ReportRecommendationSeverity.Informational;
                    if (ExecutionPercent < 25.0)
                        return ReportRecommendationSeverity.Warning;
                    else
                        return ReportRecommendationSeverity.Critical;
                }
            }

            public string Description =>
                "The finalizer thread has excessive CPU usage. This can be an indication that there " +
                "are too many resources to dispose of, and the finalizer thread isn't able to keep up " +
                "with the pace. Use the Dispose pattern to avoid putting pressure on the finalizer " +
                "thread and dispose resources deterministically.";
        }
    }

    class LocksAndWaitsComponent : ReportComponent
    {
        public class LockInfo
        {
            public string Reason { get; set; }
            public ulong Object { get; set; }
            public string ManagedObjectType { get; set; }
            public string OSObjectName { get; set; }
            public List<uint> OwnerThreadOSIds { get; } = new List<uint>();
        }

        public class ThreadInfo
        {
            public uint OSThreadId { get; set; }
            public int ManagedThreadId { get; set; }
            public List<LockInfo> Locks { get; } = new List<LockInfo>();
            [JsonIgnore] public Func<bool> IsRunningManagedCode { get; set; }
        }

        public override string Title => "Locks and waits";
        public List<ThreadInfo> Threads { get; } = new List<ThreadInfo>();

        private static ThreadInfo ThreadInfoFromThread(ClrThread thread)
        {
            return new ThreadInfo
            {
                OSThreadId = thread.OSThreadId,
                ManagedThreadId = thread.ManagedThreadId
            };
        }

        public override bool Generate(CommandExecutionContext context)
        {
            using (var target = context.CreateTemporaryDbgEngTarget())
            {
                var unifiedStackTraces = new UnifiedStackTraces(target.DebuggerInterface, context);
                var blockingObjectsStrategy = new DumpFileBlockingObjectsStrategy(context.Runtime, unifiedStackTraces, target);
                foreach (var thread in unifiedStackTraces.Threads)
                {
                    // This function is created lazily because we don't need the managed
                    // code state for each thread.
                    Func<bool> checkManagedCodeStateForThisThread = () =>
                            unifiedStackTraces.GetStackTrace(thread.EngineThreadId)
                                              .Any(f => f.Type == UnifiedStackFrameType.Managed);
                    var threadWithBlockingInfo = blockingObjectsStrategy.GetThreadWithBlockingObjects(thread);
                    var threadInfo = new ThreadInfo
                    {
                        ManagedThreadId = threadWithBlockingInfo.ManagedThreadId,
                        OSThreadId = threadWithBlockingInfo.OSThreadId,
                        IsRunningManagedCode = checkManagedCodeStateForThisThread
                    };

                    foreach (var blockingObject in threadWithBlockingInfo.BlockingObjects)
                    {
                        var lockInfo = new LockInfo
                        {
                            Reason = blockingObject.Reason.ToString()
                        };
                        if (blockingObject.Type == UnifiedBlockingType.ClrBlockingObject)
                        {
                            lockInfo.Object = blockingObject.ManagedObjectAddress;
                            lockInfo.ManagedObjectType = context.Heap.GetObjectType(lockInfo.Object)?.Name;
                        }
                        else
                        {
                            lockInfo.Object = blockingObject.Handle;
                            lockInfo.OSObjectName = blockingObject.KernelObjectName;
                        }
                        lockInfo.OwnerThreadOSIds.AddRange(blockingObject.OwnerOSThreadIds);
                        threadInfo.Locks.Add(lockInfo);
                    }

                    Threads.Add(threadInfo);
                }

                RecommendFinalizerThreadBlocked(context);
                RecommendDeadlockedThreads();
            }

            return Threads.Any();
        }

        private bool HasCycle(ThreadInfo thread, HashSet<uint> visitedThreadIds)
        {
            if (visitedThreadIds.Contains(thread.OSThreadId))
                return true;

            visitedThreadIds.Add(thread.OSThreadId);

            foreach (var @lock in thread.Locks)
            {
                foreach (var ownerThreadOSId in @lock.OwnerThreadOSIds)
                {
                    var ownerThread = Threads.SingleOrDefault(t => t.OSThreadId == ownerThreadOSId);
                    if (ownerThread == null)
                        continue;

                    if (HasCycle(ownerThread, visitedThreadIds))
                        return true;
                }
            }

            return false;
        }

        private void RecommendDeadlockedThreads()
        {
            var visitedThreadIds = new HashSet<uint>();
            foreach (var thread in Threads)
            {
                // We don't want to repeat the same deadlock multiple times
                // for the same threads that are involved. It doesn't matter
                // which thread we start from, we will end up with the same
                // deadlock "picture".
                if (visitedThreadIds.Contains(thread.OSThreadId))
                    continue;

                if (HasCycle(thread, visitedThreadIds))
                    Recommendations.Add(new DeadlockedThreads { StartingThreadOSId = thread.OSThreadId });
            }
        }

        private void RecommendFinalizerThreadBlocked(CommandExecutionContext context)
        {
            var finalizerThread = context.Runtime.Threads.SingleOrDefault(t => t.IsFinalizer);
            var threadWithLocks = Threads.SingleOrDefault(t => t.OSThreadId == finalizerThread?.OSThreadId);
            if (threadWithLocks != null && threadWithLocks.Locks.Any() && threadWithLocks.IsRunningManagedCode())
                Recommendations.Add(new FinalizerThreadBlocked(threadWithLocks));
        }

        class DeadlockedThreads : IReportRecommendation
        {
            public uint StartingThreadOSId { get; set; }

            public ReportRecommendationSeverity Severity => ReportRecommendationSeverity.Critical;

            public string Description =>
                "There are threads involved in a deadlock. Deadlocked threads will never be " +
                "able to run again, unless their wait operations time out. Break the deadlock " +
                "by inspecting the specific wait operations, and making sure locks are always " +
                "acquired in the same order.";
        }

        class FinalizerThreadBlocked : IReportRecommendation
        {
            public ReportRecommendationSeverity Severity => ReportRecommendationSeverity.Warning;

            public string Description =>
                "The finalizer thread is blocked. Long blocks in the finalizer thread may lead to " +
                "memory leaks and unmanaged resources not being cleaned up in a timely manner.";

            public int LockCount { get; private set; }
            public uint FinalizerThreadOSId { get; private set; }

            public FinalizerThreadBlocked(ThreadInfo info)
            {
                LockCount = info.Locks.Count;
                FinalizerThreadOSId = info.OSThreadId;
            }
        }
    }

    class MemoryUsageComponent : ReportComponent
    {
        public override string Title => "Memory usage";
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

        public override bool Generate(CommandExecutionContext context)
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

    class TopMemoryConsumersComponent : ReportComponent
    {
        public override string Title => "Top .NET memory consumers";

        public List<HeapTypeStatistics> TopConsumers { get; } = new List<HeapTypeStatistics>();

        public override bool Generate(CommandExecutionContext context)
        {
            if (context.TargetType == TargetType.DumpFileNoHeap)
                return false;

            var heap = context.Heap;
            var allObjects = heap.EnumerateObjectAddresses();
            TopConsumers.AddRange(heap.GroupTypesInObjectSetAndSortBySize(allObjects).Take(100));

            return true;
        }
    }

    class MemoryFragmentationComponent : ReportComponent
    {
        public class SegmentInfo
        {
            public ulong Size { get; set; }
            public ulong Committed { get; set; }
            public ulong Reserved { get; set; }
            public bool IsLargeObjectHeap { get; set; }
            public double FragmentationPercent { get; set; }
        }

        public override string Title => "Memory fragmentation";
        public ulong LargeObjectHeapSize { get; private set; }
        public double HeapFragmentationPercent { get; private set; }
        public double LargeObjectHeapFragmentationPercent { get; private set; }
        public List<SegmentInfo> HeapSegments { get; } = new List<SegmentInfo>();

        public override bool Generate(CommandExecutionContext context)
        {
            if (context.TargetType == TargetType.DumpFileNoHeap)
                return false;

            LargeObjectHeapSize = context.Heap.GetSizeByGen(3);
            var freeSpaceBySegment = context.Heap.GetFreeSpaceBySegment();
            foreach (var segment in context.Heap.Segments)
            {
                ulong free;
                if (!freeSpaceBySegment.TryGetValue(segment, out free))
                    free = 0;
                HeapSegments.Add(new SegmentInfo
                {
                    Size = segment.Length,
                    Reserved = segment.ReservedEnd - segment.Start,
                    Committed = segment.CommittedEnd - segment.Start,
                    IsLargeObjectHeap = segment.IsLarge,
                    FragmentationPercent = 100.0 * free / segment.Length
                });
            }
            HeapFragmentationPercent = 100.0 * freeSpaceBySegment.Values.Sum(l => (long)l) / context.Heap.TotalHeapSize;
            LargeObjectHeapFragmentationPercent = 100.0 * freeSpaceBySegment.Where(s => s.Key.IsLarge).Sum(l => (long)l.Value) / context.Heap.GetSizeByGen(3);

            if (LargeObjectHeapFragmentationPercent > 20.0)
                Recommendations.Add(new LargeObjectHeapFragmented { FragmentationPercent = LargeObjectHeapFragmentationPercent });

            return true;
        }

        class LargeObjectHeapFragmented : IReportRecommendation
        {
            public double FragmentationPercent { get; set; }

            public ReportRecommendationSeverity Severity
            {
                get
                {
                    if (FragmentationPercent < 30.0)
                        return ReportRecommendationSeverity.Warning;
                    else
                        return ReportRecommendationSeverity.Critical;
                }
            }

            public string Description =>
                "The large object heap is extremely fragmented. This can lead to out-of-memory " +
                "conditions, even though there is free space in the heap. In .NET 4.5.1, you " +
                "can compact the LOH on demand by using the GCSettings.LargeObjectHeapCompactionMode " +
                "property. Compacting the LOH occasionally is recommended if you experience " +
                "severe fragmentation. Alternatively, try to avoid allocating large, temporary " +
                "objects by using pooling and buffering.";
        }
    }

    class FinalizationComponent : ReportComponent
    {
        public override string Title => "Finalization statistics";

        public List<HeapTypeStatistics> ObjectsWaitingForFinalization { get; } = new List<HeapTypeStatistics>();
        public List<HeapTypeStatistics> ObjectsWithFinalizers { get; } = new List<HeapTypeStatistics>();
        public uint FinalizerThreadOSID { get; private set; }
        public ulong MemoryBytesReachableFromFinalizationQueue { get; private set; }

        // The finalizer thread stack can be obtained from the threads stack report.

        public override bool Generate(CommandExecutionContext context)
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

            if (readyForFinalization.Count > 100)
            {
                Recommendations.Add(new FinalizationQueueTooBig
                {
                    Count = (ulong)readyForFinalization.Count,
                    Size = MemoryBytesReachableFromFinalizationQueue
                });
            }
            var numberOfObjectsWithFinalizers = ObjectsWithFinalizers.Sum(group => (long)group.Size);
            if (numberOfObjectsWithFinalizers > 10000)
            {
                Recommendations.Add(new TooManyFinalizableObjects { Count = (ulong)numberOfObjectsWithFinalizers });
            }

            return true;
        }

        class FinalizationQueueTooBig : IReportRecommendation
        {
            public ulong Count { get; set; }
            public ulong Size { get; set; }

            public ReportRecommendationSeverity Severity
            {
                get
                {
                    if (Count < 1000 && Size < 1000000)
                        return ReportRecommendationSeverity.Warning;
                    else
                        return ReportRecommendationSeverity.Critical;
                }
            }

            public string Description =>
                "There are too many objects waiting for finalization. This can be an indication " +
                "that the finalizer thread can't keep up with the load, and can lead to memory leaks. " +
                "Investigate the objects waiting for finalization and try to destroy them in a more " +
                "deterministic fashion.";
        }

        class TooManyFinalizableObjects : IReportRecommendation
        {
            public ulong Count { get; set; }

            public ReportRecommendationSeverity Severity => ReportRecommendationSeverity.Warning;

            public string Description =>
                "There are many objects with finalizers that have the potential for finalization. " +
                "If these objects are properly Dispose()-d, they will not put additional pressure " +
                "on the memory system, but if they are left to the finalizer thread, they can cause " +
                "leaks and delays.";
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
                             where !type.IsAbstract
                             select (IReportComponent)Activator.CreateInstance(type);

            try
            {
                foreach (var component in components)
                {
                    if (component.Generate(context))
                        reportDocument.Components.Add(component);
                }
            }
            catch (Exception ex)
            {
                reportDocument.AnalysisResult = AnalysisResult.InternalError;
                reportDocument.AnalysisError = ex.ToString();
            }

            reportDocument.AnalysisEndTime = DateTime.Now;

            string jsonReport = JsonConvert.SerializeObject(reportDocument, Formatting.Indented, new StringEnumConverter());
            File.WriteAllText(FileName, jsonReport);
        }
    }
}
