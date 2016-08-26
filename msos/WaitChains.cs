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
            "Display only the wait chain of the thread with the specified managed thread id.")]
        public int SpecificThreadId { get; set; }

        private CommandExecutionContext _context;
        private BlockingObjectsStrategy _blockingObjectsStrategy;
        private SymbolCache _symbolCache;
        private List<UnifiedThread> _threads;
        private DataTarget _dataTarget;
        private UnifiedStackTrace _unifiedStackTrace;

        public void Execute(CommandExecutionContext context)
        {
            _context = context;
            _symbolCache = new SymbolCache();

            try
            {
                SetStrategy();

                if (SpecificThreadId != 0)
                {
                    UnifiedThread thread = SpecificThread;

                    if (SpecificThread == null)
                    {
                        _context.WriteError("There is no thread with the id '{0}'.", SpecificThreadId);
                        return;
                    }

                    DisplayChainForThread(thread, 0, new HashSet<int>());
                }
                else
                {
                    _threads.ForEach(thread => DisplayChainForThread(thread, 0, new HashSet<int>()));
                }
            }
            finally
            {
                _dataTarget?.Dispose();
            }
        }


        private void SetStrategy()
        {
            _threads = new List<UnifiedThread>();

            if (_context.TargetType == TargetType.DumpFile
                || _context.TargetType == TargetType.DumpFile)
            {
                _dataTarget = _context.CreateTemporaryDbgEngTarget();

                _unifiedStackTrace = new UnifiedStackTrace(_dataTarget.DebuggerInterface, _context);

                if (_blockingObjectsStrategy == null)
                    _blockingObjectsStrategy = new DumpFileBlockingObjectsStrategy(
                        _context.Runtime, _dataTarget);

                FetchThreads_DumpFile();
            }
            else
            {
                _blockingObjectsStrategy = new LiveProcessBlockingObjectsStrategy(_context.Runtime);
                FetchThreads_LiveProcess();
            }
        }

        #region Properties

        private UnifiedThread SpecificThread
        {
            get
            {
                return _threads.SingleOrDefault(t =>
                {
                    if (t.IsManagedThread && t.Info.ManagedThread.ManagedThreadId == SpecificThreadId)
                        return true;

                    return t.OSThreadId == SpecificThreadId;
                });
            }
        }

        private IDebugSystemObjects DebugSystemObjects => (IDebugSystemObjects)_dataTarget.DebuggerInterface;

        private IDebugControl DebugControl => (IDebugControl)_dataTarget.DebuggerInterface;

        #endregion

        private void FetchThreads_LiveProcess()
        {
            foreach (var clrThread in _context.Runtime.Threads)
            {
                var managedStack = GetManagedStackTrace(clrThread);

                var blockingObjs = new List<UnifiedBlockingObject>();

                if (clrThread.BlockingObjects?.Count > 0)
                {
                    clrThread.BlockingObjects
                        .ForEach((obj) => blockingObjs.Add(new UnifiedBlockingObject(obj)));
                }
                var unmanagedBlockingObjects = _blockingObjectsStrategy.GetUnmanagedBlockingObjects(clrThread);
                blockingObjs.AddRange(unmanagedBlockingObjects);

                _threads.Add(new UnifiedManagedThread(clrThread, managedStack, blockingObjs));
            }
        }

        private void FetchThreads_DumpFile()
        {
            uint _numThreads = 0;
            Util.VerifyHr(DebugSystemObjects.GetNumberThreads(out _numThreads));

            if (_numThreads > 0)
            {
                for (uint threadIdx = 0; threadIdx < _numThreads; ++threadIdx)
                {
                    var thread = GetUnifiedThread(threadIdx);
                    if (thread != null)
                    {
                        _threads.Add(thread);
                    }
                }
            }
        }

        private UnifiedThread GetUnifiedThread(uint threadIdx)
        {
            UnifiedThread result = null;

            ThreadInfo threadInfo = GetThreadInfo(threadIdx);

            if (threadInfo.IsManagedThread)
            {
                result = HandleManagedThread(threadInfo);
            }
            else
            {
                result = HandleUnmanagedThread(threadInfo);
            }

            return result;
        }

        private ThreadInfo GetThreadInfo(uint threadIndex)
        {
            uint[] engineThreadIds = new uint[1];
            uint[] osThreadIds = new uint[1];
            Util.VerifyHr((DebugSystemObjects).GetThreadIdsByIndex(threadIndex, 1, engineThreadIds, osThreadIds));

            ClrThread managedThread = _context.Runtime.Threads.FirstOrDefault(thread => thread.OSThreadId == osThreadIds[0]);

            return new ThreadInfo
            {
                Index = threadIndex,
                EngineThreadId = engineThreadIds[0],
                OSThreadId = osThreadIds[0],
                ManagedThread = managedThread
            };
        }

        private UnifiedUnManagedThread HandleUnmanagedThread(ThreadInfo threadInfo)
        {
            UnifiedUnManagedThread result = null;
            var unmanagedStack = _unifiedStackTrace.GetNativeStackTrace(threadInfo.EngineThreadId);

            var blockingObjects = _blockingObjectsStrategy.GetUnmanagedBlockingObjects(threadInfo, unmanagedStack);


            result = new UnifiedUnManagedThread(threadInfo, unmanagedStack, blockingObjects);

            return result;
        }

        private UnifiedManagedThread HandleManagedThread(ThreadInfo threadInfo)
        {
            var stackTrace = _unifiedStackTrace.GetStackTrace(threadInfo.EngineThreadId);

            var unmanagedStack = stackTrace.Where(stack => stack.Type == UnifiedStackFrameType.Native);
            var managedStack = stackTrace.Where(stack => stack.Type == UnifiedStackFrameType.Managed);

            var unmanagedObjs = _blockingObjectsStrategy.GetUnmanagedBlockingObjects(threadInfo, unmanagedStack.ToList());
            var managedObjs = _blockingObjectsStrategy.GetManagedBlockingObjects(threadInfo, managedStack.ToList());

            unmanagedObjs.AddRange(managedObjs);

            return new UnifiedManagedThread(threadInfo,stackTrace,unmanagedObjs);
        }

        #region Stack Trace

        private List<UnifiedStackFrame> GetManagedStackTrace(ClrThread thread)
        {
            return (from frame in thread.StackTrace
                    let sourceLocation = _symbolCache.GetFileAndLineNumberSafe(frame)
                    select new UnifiedStackFrame(frame, sourceLocation)
                    ).ToList();
        }

        #endregion

        #region Console Display

        private void DisplayChainForThread(UnifiedThread unifiedThread, int depth, HashSet<int> visitedThreadIds)
        {
            int threadId = unifiedThread.IsManagedThread ?
                unifiedThread.Info.ManagedThread.ManagedThreadId
                : (int)unifiedThread.OSThreadId;

            var commandStr = unifiedThread.IsManagedThread ? "!clrstack" : "!stack";

            var command = String.Format("~ {0}; {1}", threadId, commandStr);

            _context.WriteLink(String.Format("{0}+ Thread {1}", new string(' ', depth * 2), threadId), command);
            _context.WriteLine();

            if (visitedThreadIds.Contains(threadId))
            {
                _context.WriteLine("{0}*** DEADLOCK!", new string(' ', depth * 2));
                return;
            }

            visitedThreadIds.Add(threadId);

            if (unifiedThread.BlockingObjects != null)
            {
                DiplayThreadsBlockingObjcets(unifiedThread, depth, unifiedThread.BlockingObjects, visitedThreadIds);
            }
        }

        private void DiplayThreadsBlockingObjcets(UnifiedThread unifiedThread,
            int depth, List<UnifiedBlockingObject> blockingObjects,
            HashSet<int> visitedThreadIds)
        {
            foreach (var blockingObject in unifiedThread.BlockingObjects)
            {
                _context.Write("{0}| {1}", new string(' ', (depth + 1) * 2), blockingObject.Reason);

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
                        _context.Write(String.Format("{0:x16} {1}", blockingObject.ManagedObjectAddress, type.Name));
                    }
                    else
                    {
                        _context.Write("{0:x16}", blockingObject.ManagedObjectAddress);
                    }
                }

                _context.WriteLine();

                if (blockingObject.Owners != null)
                {
                    foreach (var owner in blockingObject.Owners)
                    {
                        if (owner == null) // ClrMD sometimes reports this nonsense
                            continue;

                        DisplayChainForThread(owner, depth + 2, visitedThreadIds);
                    }
                }
            }
        }

        #endregion
    }

    abstract class BlockingObjectsStrategy
    {
        public BlockingObjectsStrategy(ClrRuntime runtime, DataTarget dataTarget = null)
        {
            _runtime = runtime;
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

        #region Members

        protected readonly DataTarget _dataTarget;
        protected readonly IDataReader _dataReader;
        protected readonly IDebugClient _debugClient;
        protected readonly ClrRuntime _runtime;
        protected readonly StackWalkerStrategy _stackWalker;

        #endregion

        public abstract List<UnifiedBlockingObject> GetUnmanagedBlockingObjects(ClrThread thread);

        public abstract List<UnifiedBlockingObject> GetUnmanagedBlockingObjects(ThreadInfo thread, List<UnifiedStackFrame> unmanagedStack);

        internal List<UnifiedBlockingObject> GetManagedBlockingObjects(ThreadInfo thread, List<UnifiedStackFrame> stack)
        {
            //Managed Blocking objects
            List<UnifiedBlockingObject> result = new List<UnifiedBlockingObject>();
            if (thread.ManagedThread.BlockingObjects?.Count > 0)
            {
                foreach (var item in thread.ManagedThread.BlockingObjects)
                {
                    result.Add(new UnifiedBlockingObject(item));
                }
            }

            CheckForCriticalSections(result, stack);

            var arr = stack.Select(x => x?.Method).ToArray();
            foreach (var frame in stack)
            {
                if (frame?.Handles?.Count > 0)
                {
                    foreach (var handle in frame.Handles)
                    {
                        result.Add(new UnifiedBlockingObject(handle.Value, handle.ObjectName, handle.Type));
                    }
                }
            }
            return result;
        }

        internal void CheckForCriticalSections(List<UnifiedBlockingObject> list, List<UnifiedStackFrame> stack)
        {
            List<UnifiedBlockingObject> criticalSectionObjects = new List<UnifiedBlockingObject>();

            foreach (var item in stack)
            {
                UnifiedBlockingObject blockObject;

                if (_stackWalker != null && _stackWalker.GetCriticalSectionBlockingObject(item, out blockObject))
                {
                    criticalSectionObjects.Add(blockObject);
                }
            }

            if (criticalSectionObjects.Any())
            {
                if (list == null)
                    list = new List<UnifiedBlockingObject>();

                list.AddRange(criticalSectionObjects);
            }
        }

        internal virtual List<UnifiedBlockingObject> GetUnmanagedBlockingObjects(
            List<UnifiedStackFrame> unmanagedStack, uint engineThread)
        {
            unmanagedStack.ForEach(stackFrame => _stackWalker?.SetFrameParameters(stackFrame, _runtime));

            List<UnifiedBlockingObject> result = new List<UnifiedBlockingObject>();

            var framesWithHandles = unmanagedStack.Where(frame => frame.Handles?.Count > 0);

            foreach (var frame in framesWithHandles)
            {
                foreach (var handle in frame.Handles)
                {
                    result.Add(new UnifiedBlockingObject(handle.Value, handle.ObjectName, handle.Type));
                }
            }

            CheckForCriticalSections(result, unmanagedStack);

            return result;
        }

        internal List<UnifiedStackFrame> ConvertToUnified(IEnumerable<DEBUG_STACK_FRAME> stackFrames,
            ClrRuntime runtime, ThreadInfo info)
        {
            var reversed = stackFrames.Reverse();
            List<UnifiedStackFrame> stackTrace = new List<UnifiedStackFrame>();

            foreach (var frame in reversed)
            {
                var unified_frame = new UnifiedStackFrame(frame, (IDebugSymbols2)_debugClient);

                _stackWalker?.SetFrameParameters(unified_frame, runtime);

                stackTrace.Add(unified_frame);
            }

            return stackTrace;
        }
    }


    /// <summary>
    /// LiveProcessStrategy UnifiedBlockingObjects fetching:
    ///     native threads (x86): MiniDump, StackWalker
    ///     native threads (x64): MiniDump
    ///     managedthreads (x86/x64): ClrMd
    /// </summary>
    class DumpFileBlockingObjectsStrategy : BlockingObjectsStrategy
    {
        public DumpFileBlockingObjectsStrategy(ClrRuntime runtime, DataTarget dataTarget)
            : base(runtime, dataTarget)
        {
            if (_dataTarget != null)
            {
                _handles = runtime.DataTarget.DataReader.EnumerateHandles().ToList();
            }
        }

        private List<HandleInfo> _handles;

        private IEnumerable<HandleInfo> FilterByThread(ThreadInfo thread)
        {
            return _handles.Where(handle => thread.IsManagedThread ?
                    thread.ManagedThread.ManagedThreadId == handle.OwnerThreadId : handle.OwnerThreadId == thread.OSThreadId);
        }

        private IEnumerable<HandleInfo> FilterByThread(ClrThread thread)
        {
            return _handles.Where(handle => thread.ManagedThreadId == handle.OwnerThreadId);
        }

        public override List<UnifiedBlockingObject> GetUnmanagedBlockingObjects(ThreadInfo thread, List<UnifiedStackFrame> unmanagedStack)
        {
            List<UnifiedBlockingObject> result = new List<UnifiedBlockingObject>();

            foreach (var item in FilterByThread(thread))
            {
                result.Add(new UnifiedBlockingObject(item));
            }

            result.AddRange(GetUnmanagedBlockingObjects(unmanagedStack, thread.EngineThreadId));

            return result;
        }

        public override List<UnifiedBlockingObject> GetUnmanagedBlockingObjects(ClrThread thread)
        {
            List<UnifiedBlockingObject> result = new List<UnifiedBlockingObject>();

            foreach (var item in FilterByThread(thread))
            {
                result.Add(new UnifiedBlockingObject(item));
            }

            return result;
        }
    }

    /// <summary>
    /// LiveProcessStrategy UnifiedBlockingObjects fetching:
    ///     native threads (x86/x64): WCT
    ///     managed threads (x86/x64): ClrMd
    /// </summary>
    class LiveProcessBlockingObjectsStrategy : BlockingObjectsStrategy
    {
        public LiveProcessBlockingObjectsStrategy(ClrRuntime runtime)
            : base(runtime)
        {
            _wctApi = new WaitChainTraversal();
        }

        WaitChainTraversal _wctApi;

        public override List<UnifiedBlockingObject> GetUnmanagedBlockingObjects(ClrThread thread)
        {
            ThreadWCTInfo wct_threadInfo = _wctApi.GetBlockingObjects(thread.OSThreadId);
            return GetUnmanagedBlockingObjects(wct_threadInfo);
        }

        private List<UnifiedBlockingObject> GetUnmanagedBlockingObjects(ThreadWCTInfo wct_threadInfo)
        {
            List<UnifiedBlockingObject> result = new List<UnifiedBlockingObject>();

            if (wct_threadInfo?.WctBlockingObjects.Count > 0)
            {
                result = new List<UnifiedBlockingObject>();

                if (wct_threadInfo.WctBlockingObjects?.Count > 0)
                {
                    foreach (var blockingObj in wct_threadInfo.WctBlockingObjects)
                    {
                        result.Add(new UnifiedBlockingObject(blockingObj));
                    }
                }
            }

            return result;
        }

        public override List<UnifiedBlockingObject> GetUnmanagedBlockingObjects(ThreadInfo thread_info, List<UnifiedStackFrame> unmanagedStack)
        {
            //TODO: Fetch Blocking Objects with StackWalker when live thread stack will be available
            return GetUnmanagedBlockingObjects(thread_info, unmanagedStack);
        }
    }

}
