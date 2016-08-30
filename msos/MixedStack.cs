using CmdLine;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.Diagnostics.RuntimeExt;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using static msos.NativeStructs;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.DumpFileNoHeap)]
    [Verb("!mk", HelpText =
        "Display the managed and unmanaged call stack.")]
    class MixedStack : ICommand
    {
        [Option("osid", Default = 0U, HelpText =
            "The OS thread ID of the thread whose stack is to be displayed. Defaults to the current thread.")]
        public uint OSThreadId { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            if (OSThreadId == 0)
            {
                OSThreadId = context.CurrentThread.OSThreadId;
            }

            context.WriteLine("{0,-10} {1,-20} {2}", "Type", "IP", "Function");
            using (var target = context.CreateTemporaryDbgEngTarget())
            {
                var stackTracer = new UnifiedStackTraces(target.DebuggerInterface, context);
                stackTracer.PrintStackTrace(context, (from thr in stackTracer.Threads
                                                      where thr.OSThreadId == OSThreadId
                                                      select thr.Index).Single());
            }
        }
    }

    enum UnifiedStackFrameType
    {
        Managed,
        Native,
        Special
    }

    class Util
    {
        public static void VerifyHr(int hr)
        {
            if (hr != 0)
                Marshal.ThrowExceptionForHR(hr);
        }
    }

    class UnifiedStackFrame
    {
        public UnifiedStackFrameType Type { get; set; }

        public string Module { get; set; }
        public string Method { get; set; }

        public ulong OffsetInMethod { get; set; }
        public ulong InstructionPointer { get; set; }
        public ulong StackPointer { get; set; }

        public ulong FramePointer { get; private set; }
        public string SourceFileName { get; set; }
        public uint SourceLineNumber { get; set; }
        public uint SourceLineNumberEnd { get; set; }
        public uint SourceColumnNumber { get; set; }
        public uint SourceColumnNumberEnd { get; set; }

        public List<UnifiedHandle> Handles { get; } = new List<UnifiedHandle>();

        public string SourceAndLine
        {
            get
            {
                if (String.IsNullOrEmpty(SourceFileName))
                    return null;
                return String.Format("{0}:{1},{2}", SourceFileName, SourceLineNumber, SourceColumnNumber);
            }
        }

        public bool HasSource
        {
            get { return !String.IsNullOrEmpty(SourceFileName); }
        }

        public UnifiedStackFrame LinkedStackFrame { get; set; } //Used for linking managed frame to native frame

        public UnifiedStackFrame(DEBUG_STACK_FRAME nativeFrame, IDebugSymbols2 debugSymbols)
        {
            Type = UnifiedStackFrameType.Native;
            InstructionPointer = nativeFrame.InstructionOffset;
            StackPointer = nativeFrame.StackOffset;
            FramePointer = nativeFrame.FrameOffset;

            uint moduleIndex;
            ulong dummy;
            if (0 != debugSymbols.GetModuleByOffset(InstructionPointer, 0, out moduleIndex, out dummy))
            {
                //Some frames might not have modules associated with them, in which case this
                //will fail, and of course there is no function associated either. This happens
                //often with CLR JIT-compiled code.
                Module = "<Unknown>";
                Method = "<Unknown>";
                return;
            }

            StringBuilder methodName = new StringBuilder(1024);
            ulong displacement;
            uint dummy2;
            Util.VerifyHr(debugSymbols.GetNameByOffset(InstructionPointer, methodName, methodName.Capacity, out dummy2, out displacement));

            string[] parts = methodName.ToString().Split('!');
            Module = parts[0];
            if (parts.Length > 1)
            {
                Method = parts[1];
            }
            OffsetInMethod = displacement;

            uint sourceLine;
            ulong delta;
            StringBuilder sourceFile = new StringBuilder(1024);
            if (0 == debugSymbols.GetLineByOffset(InstructionPointer, out sourceLine, sourceFile, sourceFile.Capacity, out dummy2, out delta))
            {
                SourceFileName = sourceFile.ToString();
                SourceLineNumber = sourceLine;
                SourceLineNumberEnd = sourceLine;
            }
        }

        public UnifiedStackFrame(ClrStackFrame frame, Microsoft.Diagnostics.RuntimeExt.SourceLocation sourceLocation)
        {
            if (frame.Kind == ClrStackFrameType.ManagedMethod)
                Type = UnifiedStackFrameType.Managed;
            if (frame.Kind == ClrStackFrameType.Runtime)
                Type = UnifiedStackFrameType.Special;

            InstructionPointer = frame.InstructionPointer;
            StackPointer = frame.StackPointer;

            if (frame.Method == null)
                return;

            Method = frame.Method.GetFullSignature();
            if (frame.Method.Type != null)
                Module = Path.GetFileNameWithoutExtension(frame.Method.Type.Module.Name);

            OffsetInMethod = InstructionPointer - frame.Method.NativeCode;

            if (sourceLocation == null)
                return;

            SourceFileName = sourceLocation.FilePath;
            SourceLineNumber = (uint)sourceLocation.LineNumber;
            SourceLineNumberEnd = (uint)sourceLocation.LineNumberEnd;
            SourceColumnNumber = (uint)sourceLocation.ColStart;
            SourceColumnNumberEnd = (uint)sourceLocation.ColEnd;
        }
    }

    class ThreadInformation
    {
        public uint Index { get; set; }
        public uint EngineThreadId { get; set; }
        public uint OSThreadId { get; set; }
        public ClrThread ManagedThread { get; set; }
        public string Detail { get; set; }
        public bool IsManagedThread { get { return ManagedThread != null; } }
    }

    class UnifiedThread
    {
        public UnifiedThread(ThreadInformation info, IEnumerable<UnifiedBlockingObject> blockingObjects)
        {
            IsManagedThread = info.IsManagedThread;
            if (info.IsManagedThread)
            {
                ManagedThreadId = info.ManagedThread.ManagedThreadId;
            }
            Index = info.Index;
            EngineThreadId = info.EngineThreadId;
            OSThreadId = info.OSThreadId;
            Detail = info.Detail;
            BlockingObjects.AddRange(blockingObjects);
        }

        public UnifiedThread(ClrThread thread, IEnumerable<UnifiedBlockingObject> blockingObjects)
        {
            IsManagedThread = true;
            ManagedThreadId = thread.ManagedThreadId;
            OSThreadId = thread.OSThreadId;
            BlockingObjects.AddRange(blockingObjects);
        }

        public UnifiedThread(uint osThreadId)
        {
            OSThreadId = osThreadId;
        }

        public List<UnifiedBlockingObject> BlockingObjects { get; } = new List<UnifiedBlockingObject>();

        public bool IsManagedThread { get; private set; }
        public int ManagedThreadId { get; private set; }
        public uint Index { get; set; }
        public uint EngineThreadId { get; set; }
        public uint OSThreadId { get; set; }
        public string Detail { get; set; }
    }

    class UnifiedStackTraces
    {
        private IDebugClient _debugClient;
        private CommandExecutionContext _context;
        private ClrRuntime _runtime;
        private uint _numThreads;

        public UnifiedStackTraces(IDebugClient debugClient, CommandExecutionContext context)
        {
            _debugClient = debugClient;
            _context = context;
            _runtime = context.Runtime;

            Util.VerifyHr(
                ((IDebugSystemObjects)_debugClient).GetNumberThreads(out _numThreads));

            for (uint threadIdx = 0; threadIdx < _numThreads; ++threadIdx)
            {
                Threads.Add(GetThreadInfo(threadIdx));
            }
        }

        public uint NumThreads { get { return _numThreads; } }
        public List<ThreadInformation> Threads { get; } = new List<ThreadInformation>();

        private ThreadInformation GetThreadInfo(uint threadIndex)
        {
            uint[] engineThreadIds = new uint[1];
            uint[] osThreadIds = new uint[1];
            Util.VerifyHr(((IDebugSystemObjects)_debugClient).GetThreadIdsByIndex(threadIndex, 1, engineThreadIds, osThreadIds));
            ClrThread managedThread = _runtime.Threads.FirstOrDefault(thread => thread.OSThreadId == osThreadIds[0]);
            return new ThreadInformation
            {
                Index = threadIndex,
                EngineThreadId = engineThreadIds[0],
                OSThreadId = osThreadIds[0],
                ManagedThread = managedThread
            };
        }

        private List<UnifiedStackFrame> GetNativeStackTrace(uint engineThreadId)
        {
            Util.VerifyHr(((IDebugSystemObjects)_debugClient).SetCurrentThreadId(engineThreadId));

            DEBUG_STACK_FRAME[] stackFrames = new DEBUG_STACK_FRAME[200];
            uint framesFilled;
            Util.VerifyHr(((IDebugControl)_debugClient).GetStackTrace(0, 0, 0, stackFrames, stackFrames.Length, out framesFilled));

            List<UnifiedStackFrame> stackTrace = new List<UnifiedStackFrame>();
            for (uint i = 0; i < framesFilled; ++i)
            {
                stackTrace.Add(new UnifiedStackFrame(stackFrames[i], (IDebugSymbols2)_debugClient));
            }
            return stackTrace;
        }

        private List<UnifiedStackFrame> GetManagedStackTrace(ClrThread thread)
        {
            return (from frame in thread.StackTrace
                    let sourceLocation = _context.SymbolCache.GetFileAndLineNumberSafe(frame)
                    select new UnifiedStackFrame(frame, sourceLocation)
                    ).ToList();
        }

        public List<UnifiedStackFrame> GetStackTrace(uint threadIndex)
        {
            ThreadInformation threadInfo = GetThreadInfo(threadIndex);
            List<UnifiedStackFrame> unifiedStackTrace = new List<UnifiedStackFrame>();
            List<UnifiedStackFrame> nativeStackTrace = GetNativeStackTrace(threadInfo.EngineThreadId);

            if (threadInfo.IsManagedThread)
            {
                List<UnifiedStackFrame> managedStackTrace = GetManagedStackTrace(threadInfo.ManagedThread);
                int managedFrame = 0;
                for (int nativeFrame = 0; nativeFrame < nativeStackTrace.Count; ++nativeFrame)
                {
                    bool found = false;
                    for (int temp = managedFrame; temp < managedStackTrace.Count; ++temp)
                    {
                        if (nativeStackTrace[nativeFrame].InstructionPointer == managedStackTrace[temp].InstructionPointer)
                        {
                            managedStackTrace[temp].LinkedStackFrame = nativeStackTrace[nativeFrame];
                            unifiedStackTrace.Add(managedStackTrace[temp]);
                            managedFrame = temp + 1;
                            found = true;
                            break;
                        }
                        else if (managedFrame > 0)
                        {
                            // We have already seen at least one managed frame, and we're about
                            // to skip a managed frame because we didn't find a matching native
                            // frame. In this case, add the managed frame into the stack anyway.
                            unifiedStackTrace.Add(managedStackTrace[temp]);
                            managedFrame = temp + 1;
                            found = true;
                            break;
                        }
                    }
                    // We didn't find a matching managed frame, so add the native frame directly.
                    if (!found)
                        unifiedStackTrace.Add(nativeStackTrace[nativeFrame]);
                }
            }
            else
            {
                return nativeStackTrace;
            }
            return unifiedStackTrace;
        }

        public void PrintStackTrace(CommandExecutionContext context, IEnumerable<UnifiedStackFrame> stackTrace)
        {
            foreach (var frame in stackTrace)
            {
                if (frame.Type == UnifiedStackFrameType.Special)
                {
                    context.WriteLine("{0,-10}", "Special");
                    continue;
                }
                if (String.IsNullOrEmpty(frame.SourceFileName))
                {
                    context.WriteLine("{0,-10} {1,-20:x16} {2}!{3}+0x{4:x}",
                        frame.Type, frame.InstructionPointer,
                        frame.Module, frame.Method, frame.OffsetInMethod);
                }
                else
                {
                    context.WriteLine("{0,-10} {1,-20:x16} {2}!{3} [{4}:{5},{6}]",
                        frame.Type, frame.InstructionPointer,
                        frame.Module, frame.Method, frame.SourceFileName,
                        frame.SourceLineNumber, frame.SourceColumnNumber);
                }
            }
        }

        public void PrintStackTrace(CommandExecutionContext context, uint index)
        {
            var stackTrace = GetStackTrace(index);
            PrintStackTrace(context, stackTrace);
        }
    }

    class UnifiedHandle
    {
        public UnifiedHandle(ulong value, string type = null, string objectName = null)
        {
            Value = value;
            Type = type;
            ObjectName = objectName;
        }

        public ulong Value { get; private set; }
        public string Type { get; private set; }
        public string ObjectName { get; private set; }
    }


    enum UnifiedBlockingType
    {
        WaitChainInfoObject, ClrBlockingObject, DumpHandle, CriticalSectionObject, UnmanagedHandleObject
    }

    enum BlockingObjectOrigin
    {
        WaitChainTraversal, MiniDumpHandles, ClrMD, StackWalker
    }

    class UnifiedBlockingObject
    {
        private UnifiedBlockingObject(BlockingObjectOrigin source)
        {
            Origin = source;
        }

        public UnifiedBlockingObject(BlockingObject obj)
            : this(BlockingObjectOrigin.ClrMD)
        {
            foreach (var owner in obj.Owners?.Where(o => o != null) ?? new ClrThread[0])
            {
                OwnerOSThreadIds.Add(owner.OSThreadId);
            }
            foreach (var waiter in obj.Waiters?.Where(w => w != null) ?? new ClrThread[0])
            {
                WaiterOSThreadIds.Add(waiter.OSThreadId);
            }

            Reason = (UnifiedBlockingReason)((int)obj.Reason);
            RecursionCount = obj.RecursionCount;
            ManagedObjectAddress = obj.Object;

            Type = UnifiedBlockingType.ClrBlockingObject;
        }

        public UnifiedBlockingObject(ThreadWCTInfo wct) : this(BlockingObjectOrigin.WaitChainTraversal)
        {
            var thisThread = wct.WaitChain[0];
            // We could extract wait time information, context switches, and some other potentially
            // useful data from `thisThread`. For now, ignore.

            var first = wct.WaitChain[1];
            Debug.Assert(first.ObjectType != WCT_OBJECT_TYPE.WctThreadType);

            KernelObjectName = first.ObjectName;
            Reason = ConvertToUnified(first.ObjectType);
            Type = UnifiedBlockingType.WaitChainInfoObject;
            
            if (wct.WaitChain.Count > 2)
            {
                var owner = wct.WaitChain[2];
                Debug.Assert(owner.ObjectType == WCT_OBJECT_TYPE.WctThreadType);

                if (owner.OSThreadId != 0)
                    OwnerOSThreadIds.Add(owner.OSThreadId);
            }
        }

        public UnifiedBlockingObject(HandleInfo handle) : this(BlockingObjectOrigin.MiniDumpHandles)
        {
            KernelObjectName = handle.ObjectName;
            KernelObjectTypeName = handle.TypeName;
            if (handle.Type != HandleInfo.HandleType.NONE)
            {
                Reason = ConvertToUnified(handle.Type);
            }
            else if (!String.IsNullOrEmpty(handle.TypeName))
            {
                Reason = ConvertToUnified(handle.TypeName);
            }
            Type = UnifiedBlockingType.DumpHandle;
            Handle = handle.Handle;
            if (handle.OwnerThreadId != 0) // Note that this can be a thread in another process, too
            {
                OwnerOSThreadIds.Add(handle.OwnerThreadId);
            }
        }

        public UnifiedBlockingObject(CRITICAL_SECTION section, ulong address) : this(BlockingObjectOrigin.StackWalker)
        {
            OwnerOSThreadIds.Add((uint)section.OwningThread);
            Reason = UnifiedBlockingReason.CriticalSection;
            Type = UnifiedBlockingType.CriticalSectionObject;
            Handle = address;
        }

        public UnifiedBlockingObject(ulong handle, string objectName, string objectType) : this(BlockingObjectOrigin.StackWalker)
        {
            Handle = handle;
            KernelObjectName = objectName;
            KernelObjectTypeName = objectType;
            Type = UnifiedBlockingType.UnmanagedHandleObject;
            Reason = ConvertToUnified(objectType);
        }

        public BlockingObjectOrigin Origin { get; private set; }

        public UnifiedBlockingType Type { get; private set; }

        public List<uint> OwnerOSThreadIds { get; } = new List<uint>();

        public bool HasOwnershipInformation => OwnerOSThreadIds.Count > 0;

        public UnifiedBlockingReason Reason { get; private set; } = UnifiedBlockingReason.Unknown;

        public List<uint> WaiterOSThreadIds { get; } = new List<uint>();

        public int RecursionCount { get; private set; }

        public ulong ManagedObjectAddress { get; private set; }

        public string KernelObjectName { get; private set; }

        public string KernelObjectTypeName { get; private set; }

        public ulong Handle { get; private set; }

        const int BLOCK_REASON_WCT_SECTION_START_INDEX = 9;

        /// <summary>
        /// Converts the object type of a handle to UnifiedBlockingReason enum value.
        /// </summary>
        private static UnifiedBlockingReason ConvertToUnified(string objectType)
        {
            UnifiedBlockingReason result = UnifiedBlockingReason.Unknown;

            switch (objectType)
            {
                case "Thread": result = UnifiedBlockingReason.Thread; break;
                case "Job": result = UnifiedBlockingReason.Job; break;
                case "File": result = UnifiedBlockingReason.File; break;
                case "Semaphore": result = UnifiedBlockingReason.Semaphore; break;
                case "Mutex": result = UnifiedBlockingReason.Mutex; break;
                case "Section": result = UnifiedBlockingReason.CriticalSection; break;
                case "Mutant": result = UnifiedBlockingReason.Mutex; break;
                case "ALPC Port": result = UnifiedBlockingReason.Alpc; break;
                case "Process": result = UnifiedBlockingReason.ProcessWait; break;
                case "Unknown": result = UnifiedBlockingReason.Unknown; break;
                case "None": result = UnifiedBlockingReason.None; break;
                case "Timer": result = UnifiedBlockingReason.Timer; break;
                case "Event": result = UnifiedBlockingReason.Event; break;
            }
            return result;
        }

        /// <summary>
        /// Converts HandleType enum value to UnifiedBlockingReason enum value.
        /// </summary>
        private UnifiedBlockingReason ConvertToUnified(HandleInfo.HandleType type)
        {
            UnifiedBlockingReason result = UnifiedBlockingReason.Unknown;
            switch (type)
            {
                case HandleInfo.HandleType.NONE: result = UnifiedBlockingReason.None; break;
                case HandleInfo.HandleType.THREAD: result = UnifiedBlockingReason.ThreadWait; break;
                case HandleInfo.HandleType.MUTEX: result = UnifiedBlockingReason.Mutex; break;
                case HandleInfo.HandleType.PROCESS: result = UnifiedBlockingReason.ProcessWait; break;
                case HandleInfo.HandleType.EVENT: result = UnifiedBlockingReason.Event; break;
                case HandleInfo.HandleType.SECTION: result = UnifiedBlockingReason.MemorySection; break;
            }
            return result;
        }

        /// <summary>
        /// Converts WCT_OBJECT_TYPE enum value to UnifiedBlockingReason enum value.
        /// </summary>
        private UnifiedBlockingReason ConvertToUnified(WCT_OBJECT_TYPE type)
        {
            var wctIndex = (int)type;
            return (UnifiedBlockingReason)(BLOCK_REASON_WCT_SECTION_START_INDEX + wctIndex);
        }
    }

    enum UnifiedBlockingReason
    {
        // Managed blocking reason values.
        None = 0,
        Unknown = 1,
        Monitor = 2,
        MonitorWait = 3,
        WaitOne = 4,
        WaitAll = 5,
        WaitAny = 6,
        ThreadJoin = 7,
        ReaderAcquired = 8,
        WriterAcquired = 9,

        // WCT_OBJECT_TYPE and handle types
        CriticalSection = 10,
        SendMessage = 11,
        Mutex = 12,
        Alpc = 13,
        Com = 14,
        ThreadWait = 15,
        ProcessWait = 16,
        Thread = 17,
        ComActivation = 18,
        UnknownType = Unknown,
        File = 19,
        Job = 20,
        Semaphore = 21,
        Event = 22,        
        Timer = 23,
        MemorySection = 24
    }
}
