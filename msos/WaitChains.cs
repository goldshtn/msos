using CmdLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Runtime.Interop;
using System.Runtime.InteropServices;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.LiveProcess)]
    [Verb("!waits", HelpText = "Displays wait chain information and detects deadlocks.")]
    class WaitChains : ICommand
    {
        [Option("thread", HelpText =
            "Display only the wait chain of the thread with the specified OS thread id.")]
        public int SpecificOSThreadId { get; set; }

        private CommandExecutionContext _context;
        private BlockingObjectsStrategy _blockingObjectsStrategy;
        private List<UnifiedThread> _threads = new List<UnifiedThread>();
        private DataTarget _temporaryDbgEngTarget;
        private UnifiedStackTraces _unifiedStackTraces;

        public void Execute(CommandExecutionContext context)
        {
            _context = context;

            try
            {
                SetStrategy();

                if (SpecificOSThreadId != 0)
                {
                    UnifiedThread thread = SpecificThread;

                    if (SpecificThread == null)
                    {
                        _context.WriteErrorLine("There is no thread with the id '{0}'.", SpecificOSThreadId);
                        return;
                    }

                    DisplayChainForThread(thread, 0, new HashSet<uint>());
                }
                else
                {
                    _threads.ForEach(thread => DisplayChainForThread(thread, 0, new HashSet<uint>()));
                }
            }
            finally
            {
                _temporaryDbgEngTarget?.Dispose();
            }
        }


        private void SetStrategy()
        {
            if (_context.TargetType == TargetType.DumpFile
                || _context.TargetType == TargetType.DumpFile)
            {
                _temporaryDbgEngTarget = _context.CreateTemporaryDbgEngTarget();
                _unifiedStackTraces = new UnifiedStackTraces(_temporaryDbgEngTarget.DebuggerInterface, _context);
                _blockingObjectsStrategy = new DumpFileBlockingObjectsStrategy(_context.Runtime, _unifiedStackTraces, _temporaryDbgEngTarget);

                _threads.AddRange(_unifiedStackTraces.Threads.Select(ti => _blockingObjectsStrategy.GetThreadWithBlockingObjects(ti)));
            }
            else
            {
                _blockingObjectsStrategy = new LiveProcessBlockingObjectsStrategy(_context.Runtime);

                // Currently, we are only enumerating the managed threads because we don't have 
                // an alternative source of information for threads in live processes. In the future,
                // we can consider using System.Diagnostics or some other means of enumerating threads
                // in live processes.
                _threads.AddRange(_context.Runtime.Threads.Select(thr => _blockingObjectsStrategy.GetThreadWithBlockingObjects(thr)));
            }
        }

        private UnifiedThread SpecificThread => _threads.SingleOrDefault(t => t.OSThreadId == SpecificOSThreadId);

        private void DisplayChainForThread(UnifiedThread unifiedThread, int depth, HashSet<uint> visitedThreadIds)
        {
            if (unifiedThread.IsManagedThread)
            {
                var command = String.Format("~ {0}; !mk", unifiedThread.ManagedThreadId);
                _context.WriteLink(String.Format("{0}+ OS Thread {1}", new string(' ', depth * 2), unifiedThread.OSThreadId), command);
            }
            else
            {
                _context.Write("+ OS Thread {0}", unifiedThread.OSThreadId);
            }
            _context.WriteLine();

            if (visitedThreadIds.Contains(unifiedThread.OSThreadId))
            {
                _context.WriteLine("{0}*** DEADLOCK!", new string(' ', depth * 2));
                return;
            }

            visitedThreadIds.Add(unifiedThread.OSThreadId);

            DisplayThreadBlockingObjects(unifiedThread, depth, unifiedThread.BlockingObjects, visitedThreadIds);
        }

        private void DisplayThreadBlockingObjects(UnifiedThread unifiedThread,
            int depth, List<UnifiedBlockingObject> blockingObjects, HashSet<uint> visitedThreadIds)
        {
            foreach (var blockingObject in unifiedThread.BlockingObjects)
            {
                _context.Write("{0}| {1} {2}", new string(' ', (depth + 1) * 2), blockingObject.Reason, blockingObject.ReasonDescription);

                if (!String.IsNullOrEmpty(blockingObject.KernelObjectName))
                {
                    _context.Write(
                        String.Format("{0:x16} {1} {2}", blockingObject.Handle,
                        blockingObject.KernelObjectTypeName, blockingObject.KernelObjectName));
                }

                if (blockingObject.Type == UnifiedBlockingType.ClrBlockingObject)
                {
                    var type = _context.Heap.GetObjectType(blockingObject.ManagedObjectAddress);
                    if (type != null && !String.IsNullOrEmpty(type.Name))
                    {
                        _context.WriteLink(
                            String.Format("{0:x16} {1}", blockingObject.ManagedObjectAddress, type.Name),
                            String.Format("!do {0:x16}", blockingObject.ManagedObjectAddress));
                    }
                    else
                    {
                        _context.Write("{0:x16}", blockingObject.ManagedObjectAddress);
                    }
                }

                _context.WriteLine();

                foreach (var owner in blockingObject.OwnerOSThreadIds)
                {
                    var thread = _threads.SingleOrDefault(t => t.OSThreadId == owner);
                    // We won't necessarily have that thread on our list because it might be 
                    // a thread in another process, e.g. for mutexes.
                    if (thread != null)
                        DisplayChainForThread(thread, depth + 2, visitedThreadIds);
                }
            }
        }
    }

    abstract class BlockingObjectsStrategy
    {
        public BlockingObjectsStrategy(
            ClrRuntime runtime, UnifiedStackTraces unifiedStackTraces = null, DataTarget dataTarget = null)
        {
            _runtime = runtime;
            _unifiedStackTraces = unifiedStackTraces;
            _dataTarget = dataTarget;

            if (_dataTarget != null)
            {
                if (_dataTarget.Architecture == Architecture.X86)
                {
                    _stackWalker = new StackWalkerStrategy_x86(_runtime);
                }

                _dataReader = _dataTarget.DataReader;
                _debugClient = _dataTarget.DebuggerInterface;
            }
        }

        protected readonly UnifiedStackTraces _unifiedStackTraces;
        protected readonly DataTarget _dataTarget;
        protected readonly IDataReader _dataReader;
        protected readonly IDebugClient _debugClient;
        protected readonly ClrRuntime _runtime;
        protected readonly StackWalkerStrategy _stackWalker;

        public UnifiedThread GetThreadWithBlockingObjects(ClrThread thread)
        {
            var blockingObjects = GetUnmanagedBlockingObjects(thread.OSThreadId);
            blockingObjects.AddRange(GetManagedBlockingObjects(thread.OSThreadId));
            return new UnifiedThread(thread, blockingObjects);
        }

        public UnifiedThread GetThreadWithBlockingObjects(ThreadInformation threadInfo)
        {
            var blockingObjects = GetUnmanagedBlockingObjects(threadInfo.OSThreadId);
            if (threadInfo.IsManagedThread)
            {
                blockingObjects.AddRange(GetManagedBlockingObjects(threadInfo.OSThreadId));
            }
            return new UnifiedThread(threadInfo, blockingObjects);
        }

        public virtual List<UnifiedBlockingObject> GetUnmanagedBlockingObjects(uint osThreadId)
        {
            var result = new List<UnifiedBlockingObject>();
            if (_unifiedStackTraces != null && _stackWalker != null)
            {
                var threadInfo = _unifiedStackTraces.Threads.SingleOrDefault(ti => ti.OSThreadId == osThreadId);
                if (threadInfo != null)
                {
                    var stack = _unifiedStackTraces.GetStackTrace(threadInfo.EngineThreadId);
                    foreach (var frame in stack)
                    {
                        _stackWalker.SetFrameParameters(frame);

                        UnifiedBlockingObject blockingObject;
                        if (_stackWalker.GetCriticalSectionBlockingObject(frame, out blockingObject))
                            result.Add(blockingObject);
                        else if (_stackWalker.GetThreadSleepBlockingObject(frame, out blockingObject))
                            result.Add(blockingObject);

                        result.AddRange(frame.Handles.Select(GetUnifiedBlockingObjectForHandle));
                    }
                }
            }
            return result;
        }

        public List<UnifiedBlockingObject> GetManagedBlockingObjects(uint osThreadId)
        {
            var thread = _runtime.Threads.SingleOrDefault(t => t.OSThreadId == osThreadId);
            if (thread == null)
                return new List<UnifiedBlockingObject>();

            return thread.BlockingObjects.Select(bo => new UnifiedBlockingObject(bo)).ToList();
        }

        protected virtual UnifiedBlockingObject GetUnifiedBlockingObjectForHandle(UnifiedHandle handle)
        {
            return new UnifiedBlockingObject(handle.Value, handle.ObjectName, handle.Type);
        }
    }


    /// <summary>
    /// For dump files, obtain wait information from the following sources:
    ///     native threads (x86): Minidump handles, stack walker
    ///     native threads (x64): Minidump handles
    ///     managed threads (x86/x64): ClrMD
    /// </summary>
    class DumpFileBlockingObjectsStrategy : BlockingObjectsStrategy
    {
        public DumpFileBlockingObjectsStrategy(ClrRuntime runtime, UnifiedStackTraces unifiedStackTraces, DataTarget dataTarget)
            : base(runtime, unifiedStackTraces, dataTarget)
        {
            if (_dataTarget != null)
            {
                try
                {
                    _handles = runtime.DataTarget.DataReader.EnumerateHandles().ToList();
                }
                catch (ClrDiagnosticsException)
                {
                    // The dump file probably doesn't contain the handle stream.
                }
            }
        }

        private List<HandleInfo> _handles = new List<HandleInfo>();

        protected override UnifiedBlockingObject GetUnifiedBlockingObjectForHandle(UnifiedHandle handle)
        {
            var handleInfo = _handles.SingleOrDefault(h => h.Handle == handle.Value);
            if (handleInfo == null)
                return base.GetUnifiedBlockingObjectForHandle(handle);

            return new UnifiedBlockingObject(handleInfo);
        }
    }

    /// <summary>
    /// For live processes, obtain wait information from the following sources:
    ///     native threads (x86/x64): WCT
    ///     managed threads (x86/x64): ClrMD
    /// </summary>
    class LiveProcessBlockingObjectsStrategy : BlockingObjectsStrategy
    {
        public LiveProcessBlockingObjectsStrategy(ClrRuntime runtime) : base(runtime)
        {
        }

        private WaitChainTraversal _wctApi = new WaitChainTraversal();

        public override List<UnifiedBlockingObject> GetUnmanagedBlockingObjects(uint osThreadId)
        {
            var blockingObjects = base.GetUnmanagedBlockingObjects(osThreadId);
            ThreadWCTInfo wctInfo = _wctApi.GetBlockingObjects(osThreadId);
            if (wctInfo != null && wctInfo.WaitChain.Count > 1)
            {   // The first node is always the current thread, which is not that interesting.
                blockingObjects.Add(new UnifiedBlockingObject(wctInfo));
            }
            return blockingObjects;
        }
    }

}
