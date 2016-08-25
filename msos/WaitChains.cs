using CmdLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Runtime.Interop;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

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

        public void Execute(CommandExecutionContext context)
        {
            _context = context;
            _symbolCache = new SymbolCache();

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

            if (_context.NativeDbgEngTarget != null)
                _context.ExitDbgEngNativeMode();

        }

        private void SetStrategy()
        {
            _threads = new List<UnifiedThread>();

            if (_context.TargetType == TargetType.DumpFile || _context.TargetType == TargetType.DumpFile)
            {
                if (_context.NativeDbgEngTarget == null)
                    _context.EnterDbgEngNativeMode();

                if (_blockingObjectsStrategy == null)
                    _blockingObjectsStrategy = new DumpFileBlockingObjectsStrategy(_context);

                FetchThreads_DumpFile();
            }
            else
            {
                _blockingObjectsStrategy = new LiveProcessBlockingObjectsStrategy(_context);
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

        private IDebugSystemObjects DebugSystemObjects => (IDebugSystemObjects)_context
            .NativeDbgEngTarget.DebuggerInterface;

        private IDebugControl DebugControl => (IDebugControl)_context
            .NativeDbgEngTarget.DebuggerInterface;

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

        private UnifiedUnManagedThread HandleUnmanagedThread(ThreadInfo specific_info)
        {
            UnifiedUnManagedThread result = null;
            var unmanagedStack = GetNativeStackTrace(specific_info);

            var blockingObjects = _blockingObjectsStrategy.GetUnmanagedBlockingObjects(specific_info, unmanagedStack);


            result = new UnifiedUnManagedThread(specific_info, unmanagedStack, blockingObjects);

            return result;
        }

        private UnifiedManagedThread HandleManagedThread(ThreadInfo specific_info)
        {
            var unmanagedStack = GetNativeStackTrace(specific_info);
            var managedStack = GetManagedStackTrace(specific_info.ManagedThread);

            var unmanagedObjs = _blockingObjectsStrategy.GetUnmanagedBlockingObjects(specific_info, unmanagedStack);
            var managedObjs = _blockingObjectsStrategy.GetManagedBlockingObjects(specific_info, managedStack);

            unmanagedObjs.AddRange(managedObjs);

            return new UnifiedManagedThread(specific_info, managedStack, unmanagedStack, unmanagedObjs);
        }

        #region Stack Trace

        private unsafe List<UnifiedStackFrame> GetNativeStackTrace(ThreadInfo info)
        {
            if (_context.TargetType == TargetType.LiveProcess)
                return null;

            Util.VerifyHr((DebugSystemObjects).SetCurrentThreadId(info.EngineThreadId));

            DEBUG_STACK_FRAME[] stackFrames = new DEBUG_STACK_FRAME[400];
            uint framesFilled;

            DEBUG_STACK_FRAME frame = new DEBUG_STACK_FRAME();
            Util.VerifyHr((DebugControl).GetStackTrace(0, 0, 0,
                            stackFrames, Marshal.SizeOf(frame), out framesFilled));

            var frames = stackFrames.Take((int)framesFilled);
            return _blockingObjectsStrategy.ConvertToUnified(frames, _context.Runtime, info);
        }

        private List<UnifiedStackFrame> GetManagedStackTrace(ClrThread thread)
        {
            return (from frame in thread.StackTrace
                    let sourceLocation = _symbolCache.GetFileAndLineNumberSafe(frame)
                    select new UnifiedStackFrame(frame, sourceLocation)
                    ).ToList();
        }

        #endregion

        #region Console Display

        private void DisplayChainForThread(UnifiedThread thread, int depth, HashSet<int> visitedThreadIds)
        {
            if (thread.IsManagedThread)
            {
                DisplayManagedThread_ChainAux(thread, 0, new HashSet<int>());
            }
            else
            {
                DisplayUnManagedThread_ChainAux(thread, 0, new HashSet<int>());
            }
        }

        private void DisplayManagedThread_ChainAux(UnifiedThread unified_thread, int depth, HashSet<int> visitedThreadIds)
        {
            var thread = unified_thread.Info.ManagedThread;

            _context.WriteLink(
                String.Format("{0}+ Thread {1}", new string(' ', depth * 2), thread.ManagedThreadId),
                String.Format("~ {0}; !clrstack", thread.ManagedThreadId));
            _context.WriteLine();

            if (visitedThreadIds.Contains(thread.ManagedThreadId))
            {
                _context.WriteLine("{0}*** DEADLOCK!", new string(' ', depth * 2));
                return;
            }
            visitedThreadIds.Add(thread.ManagedThreadId);

            if (unified_thread.BlockingObjects != null)
            {
                foreach (var blockingObject in unified_thread.BlockingObjects)
                {
                    
                    _context.Write("{0}| {1}", new string(' ', (depth + 1) * 2), blockingObject.Reason);

                    if (!String.IsNullOrEmpty(blockingObject.KernelObjectName))
                    {
                        _context.WriteLink(
                            String.Format("{0:x16} {1} {2}", blockingObject.Handle,
                            blockingObject.KernelObjectTypeName, blockingObject.KernelObjectName),
                            String.Format("!do {0:x16}", blockingObject.Handle));
                    }

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
        }

        private void DisplayUnManagedThread_ChainAux(UnifiedThread thread, int depth, HashSet<int> visitedThreadIds)
        {
            _context.WriteLink(
                String.Format("{0}+ Thread {1}", new string(' ', depth * 2), thread.OSThreadId),
                String.Format("~ {0}; !stack", thread.OSThreadId));

            _context.WriteLine();

            if (visitedThreadIds.Contains((int)thread.OSThreadId))
            {
                _context.WriteLine("{0}*** DEADLOCK!", new string(' ', depth * 2));
                return;
            }

            visitedThreadIds.Add((int)thread.OSThreadId);

            if (thread.BlockingObjects != null)
            {
                foreach (var blockingObject in thread.BlockingObjects)
                {
                    _context.Write("{0}| {1} ", new string(' ', (depth + 1) * 2), blockingObject.Reason);

                    if (!String.IsNullOrEmpty(blockingObject.KernelObjectName))
                    {
                        _context.WriteLink(String.Format("{0:x16} {1} {2}", blockingObject.Handle,
                            blockingObject.KernelObjectTypeName, blockingObject.KernelObjectName),
                            String.Format("!do {0:x16}", blockingObject.Handle));
                    }

                    _context.WriteLine();

                    if (blockingObject.Owners != null)
                    {
                        foreach (var owner in blockingObject.Owners)
                        {
                            if (owner == null)
                                continue;

                            DisplayUnManagedThread_ChainAux(owner, depth + 2, visitedThreadIds);
                        }
                    }
                }
            }
        }

        #endregion
    }

    abstract class BlockingObjectsStrategy
    {
        public BlockingObjectsStrategy(CommandExecutionContext context)
        {
            _runtime = context.Runtime;

            if (_runtime.DataTarget.Architecture == Architecture.X86)
            {
                _stackWalker = new StackWalkerStrategy_x86(_runtime);
            }
        }

        #region Members

        protected IDataReader _dataReader;
        protected IDebugClient _debugClient;
        protected ClrRuntime _runtime;
        private CommandExecutionContext context;
        protected StackWalkerStrategy _stackWalker;

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
        public DumpFileBlockingObjectsStrategy(CommandExecutionContext context) : base(context)
        {
            _miniDump = new DumpReader(context.DumpFile);

            if (context.NativeDbgEngTarget != null)
            {
                context.EnterDbgEngNativeMode();
                _dataReader = context.NativeDbgEngTarget.DataReader;
                _debugClient = context.NativeDbgEngTarget.DebuggerInterface;
            }

            _handles = _miniDump.EnumerateHandles().ToList();
        }

        private DumpReader _miniDump;
        private List<DumpHandle> _handles;

        private IEnumerable<DumpHandle> FilterByThread(ThreadInfo thread)
        {
            return _handles.Where(handle => thread.IsManagedThread ?
                    thread.ManagedThread.ManagedThreadId == handle.OwnerThreadId : handle.OwnerThreadId == thread.OSThreadId);
        }

        private IEnumerable<DumpHandle> FilterByThread(ClrThread thread)
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
        public LiveProcessBlockingObjectsStrategy(CommandExecutionContext context) : base(context)
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
