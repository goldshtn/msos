using CmdLine;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;
using Microsoft.Diagnostics.RuntimeExt;
using System;
using System.Collections.Generic;
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
                var stackTracer = new UnifiedStackTrace(target.DebuggerInterface, context);
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

        public ulong FrameOffset { get; private set; }
        public string SourceFileName { get; set; }
        public uint SourceLineNumber { get; set; }
        public uint SourceLineNumberEnd { get; set; }
        public uint SourceColumnNumber { get; set; }
        public uint SourceColumnNumberEnd { get; set; }

        public List<byte[]> NativeParams { get; set; }
        public List<UnifiedHandle> Handles { get; set; }

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
            FrameOffset = nativeFrame.FrameOffset;

            Type = UnifiedStackFrameType.Native;
            InstructionPointer = nativeFrame.InstructionOffset;
            StackPointer = nativeFrame.StackOffset;

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

        public UnifiedStackFrame(ClrStackFrame frame, SourceLocation sourceLocation)
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

    class ThreadInfo
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
        public UnifiedThread(ThreadInfo info)
        {
            IsManagedThread = info.IsManagedThread;
            Index = info.Index;
            EngineThreadId = info.EngineThreadId;
            OSThreadId = info.OSThreadId;
            Detail = info.Detail;
            Info = info;
        }

        public UnifiedThread(uint owningThreadId)
        {
            this.OSThreadId = owningThreadId;
        }
        public ThreadInfo Info { get; private set; }
        public List<UnifiedStackFrame> StackTrace { get; protected set; }
        public List<UnifiedBlockingObject> BlockingObjects { get; protected set; }

        public bool IsManagedThread { get; protected set; }
        public uint Index { get; set; }
        public uint EngineThreadId { get; set; }
        public uint OSThreadId { get; set; }
        public string Detail { get; set; }
    }

    class UnifiedManagedThread : UnifiedThread
    {
        public UnifiedManagedThread(ThreadInfo info, List<UnifiedStackFrame> managedStack, List<UnifiedStackFrame> unManagedStack, List<UnifiedBlockingObject> blockingObjects) : base(info)
        {
            StackTrace = new List<UnifiedStackFrame>();

            if (managedStack != null)
            {
                StackTrace.AddRange(managedStack);
            }

            if (unManagedStack != null)
            {
                StackTrace.AddRange(unManagedStack);
            }
            BlockingObjects = blockingObjects;
        }

        public UnifiedManagedThread(ClrThread thread)
            : base(new ThreadInfo()
            {
                OSThreadId = thread.OSThreadId,
                ManagedThread = thread
            })
        {
            //TODO: complete logic -> used with Blocking object Wiater    
        }

        public UnifiedManagedThread(ClrThread thread, List<UnifiedBlockingObject> blockingObjs)
            : base(new ThreadInfo()
            {
                OSThreadId = thread.OSThreadId,
                ManagedThread = thread
            })
        {
            BlockingObjects = blockingObjs;
        }

        public UnifiedManagedThread(ClrThread thread,
            List<UnifiedStackFrame> managedStack,
            List<UnifiedBlockingObject> blockingObjs)
            : base(new ThreadInfo()
            {
                OSThreadId = thread.OSThreadId,
                ManagedThread = thread
            })
        {
            StackTrace = new List<UnifiedStackFrame>();

            if (managedStack != null)
            {
                StackTrace.AddRange(managedStack);
            }

            BlockingObjects = blockingObjs;
        }
    }

    class UnifiedStackTrace
    {
        private IDebugClient _debugClient;
        private CommandExecutionContext _context;
        private ClrRuntime _runtime;
        private uint _numThreads;


        public UnifiedStackTrace(IDebugClient debugClient, CommandExecutionContext context)
        {
            _debugClient = debugClient;
            _context = context;
            _runtime = context.Runtime;

            Util.VerifyHr(
                ((IDebugSystemObjects)_debugClient).GetNumberThreads(out _numThreads));

            var threads = new List<ThreadInfo>();
            for (uint threadIdx = 0; threadIdx < _numThreads; ++threadIdx)
            {
                threads.Add(GetThreadInfo(threadIdx));
            }
            Threads = threads;
        }

        public uint NumThreads { get { return _numThreads; } }
        public IEnumerable<ThreadInfo> Threads { get; private set; }

        public ThreadInfo GetThreadInfo(uint threadIndex)
        {
            uint[] engineThreadIds = new uint[1];
            uint[] osThreadIds = new uint[1];
            Util.VerifyHr(((IDebugSystemObjects)_debugClient).GetThreadIdsByIndex(threadIndex, 1, engineThreadIds, osThreadIds));
            ClrThread managedThread = _runtime.Threads.FirstOrDefault(thread => thread.OSThreadId == osThreadIds[0]);
            return new ThreadInfo
            {
                Index = threadIndex,
                EngineThreadId = engineThreadIds[0],
                OSThreadId = osThreadIds[0],
                ManagedThread = managedThread
            };
        }
        public List<UnifiedStackFrame> GetNativeStackTrace(uint engineThreadId)
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

        public List<UnifiedStackFrame> GetManagedStackTrace(ClrThread thread)
        {
            return (from frame in thread.StackTrace
                    let sourceLocation = _context.SymbolCache.GetFileAndLineNumberSafe(frame)
                    select new UnifiedStackFrame(frame, sourceLocation)
                    ).ToList();
        }

        public List<UnifiedStackFrame> GetStackTrace(uint threadIndex)
        {
            ThreadInfo threadInfo = GetThreadInfo(threadIndex);
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
    class UnifiedUnManagedThread : UnifiedThread
    {
        public UnifiedUnManagedThread(ThreadInfo info, List<UnifiedStackFrame> unmanagedStack, List<UnifiedBlockingObject> blockingObjects) : base(info)
        {
            BlockingObjects = blockingObjects;
            StackTrace = unmanagedStack;
        }
    }

    public enum UnifiedHandleType
    {
        Handle, CriticalSection
    }

    public class UnifiedHandle
    {
        public UnifiedHandle(ulong uid, UnifiedHandleType unifiedType = UnifiedHandleType.Handle, string type = null, string objectName = null)
        {
            Id = uid;
            Type = type;
            ObjectName = objectName;
        }

        public ulong Id { get; private set; }
        public string Type { get; private set; }
        public UnifiedHandleType UnifiedHandleType { get; private set; }
        public string ObjectName { get; private set; }
    }


    public enum UnifiedBlockingType
    {
        WaitChainInfoObject, ClrBlockingObject, MiniDumpHandle, CriticalSectionObject, UnmanagedHandleObject
    }

    public enum OriginSource
    {
        WCT, MiniDump, ClrMD, StackWalker, ThreadContextRegisters
    }

    public class UnifiedBlockingObject
    {
        private UnifiedBlockingObject(OriginSource source)
        {
            Origin = source;
        }

        public UnifiedBlockingObject(BlockingObject obj) : this(OriginSource.ClrMD)
        {

            SetOwners(obj);
            SetWaiters(obj);

            Reason = (UnifiedBlockingReason)((int)obj.Reason);
            RecursionCount = obj.RecursionCount;
            ManagedObjectAddress = obj.Object;
            KernelObjectName = null;

            Type = UnifiedBlockingType.ClrBlockingObject;

        }

        internal UnifiedBlockingObject(WaitChainInfoObject obj) : this(OriginSource.WCT)
        {
            KernelObjectName = obj.ObjectName;
            Reason = obj.UnifiedType;
            Type = UnifiedBlockingType.WaitChainInfoObject;
        }

        internal UnifiedBlockingObject(MiniDumpHandle handle)
            : this(OriginSource.MiniDump)
        {
            KernelObjectName = handle.ObjectName;
            KernelObjectTypeName = handle.TypeName;
            Reason = handle.UnifiedType;
            Type = UnifiedBlockingType.MiniDumpHandle;
            Handle = handle.Handle;
        }

        internal UnifiedBlockingObject(CRITICAL_SECTION section, ulong handle)
            : this(OriginSource.StackWalker)
        {
            Owners = new List<UnifiedThread>();
            Owners.Add(new UnifiedThread((uint)section.OwningThread));
            Reason = UnifiedBlockingReason.CriticalSection;
            Type = UnifiedBlockingType.CriticalSectionObject;
            Handle = handle;
        }

        public UnifiedBlockingObject(ulong handle, string objectName, string objectType)
            : this(OriginSource.StackWalker)
        {
            Owners = new List<UnifiedThread>();
            Handle = handle;
            KernelObjectName = objectName;
            KernelObjectTypeName = objectType;
            Type = UnifiedBlockingType.UnmanagedHandleObject;
            Reason = ConvertToUnified(objectType);
        }

        public UnifiedBlockingObject(ulong handle, UnifiedBlockingType type)
            : this(OriginSource.ThreadContextRegisters)
        {
            Handle = handle;
            Type = type;
        }

        private void SetWaiters(BlockingObject item)
        {
            if (item.Waiters?.Count > 0)
            {
                Owners = new List<UnifiedThread>();
                foreach (var waiter in item.Waiters)
                {
                    this.Owners.Add(new UnifiedManagedThread(waiter));
                }
            }
        }

        private void SetOwners(BlockingObject item)
        {
            if (item.Owners?.Count > 0)
            {
                Owners = new List<UnifiedThread>();
                foreach (var owner in item.Owners)
                {
                    if (owner != null)
                    {
                        this.Owners.Add(new UnifiedManagedThread(owner));
                    }
                }
            }
        }

        public OriginSource Origin { get; private set; }
        public UnifiedBlockingType Type { get; private set; }

        internal List<UnifiedThread> Owners { get; private set; }

        public bool HasOwnershipInformation { get { return Owners != null && Owners.Count > 0; } }

        public UnifiedBlockingReason Reason { get; private set; } = UnifiedBlockingReason.Unknown;

        internal List<UnifiedThread> Waiters { get; private set; }

        public int RecursionCount { get; private set; }

        public ulong ManagedObjectAddress { get; private set; }

        public string KernelObjectName { get; private set; }

        public string KernelObjectTypeName { get; private set; }
        public ulong Handle { get; private set; }

        public const int BLOCK_REASON_WCT_SECTION_START_INDEX = 9;

        private static UnifiedBlockingReason ConvertToUnified(string objectType)
        {
            UnifiedBlockingReason result = UnifiedBlockingReason.Unknown;
            switch (objectType)
            {
                case "Thread":
                    result = UnifiedBlockingReason.Thread;
                    break;
                case "Job":
                    result = UnifiedBlockingReason.Job;
                    break;
                case "File":
                    result = UnifiedBlockingReason.File;
                    break;
                case "Semaphore":
                    result = UnifiedBlockingReason.Semaphore;
                    break;
                case "Mutex":
                    result = UnifiedBlockingReason.Mutex;
                    break;
                case "Section":
                    result = UnifiedBlockingReason.CriticalSection;
                    break;
                case "Mutant":
                    result = UnifiedBlockingReason.Mutex;
                    break;
                case "ALPC Port":
                    result = UnifiedBlockingReason.Alpc;
                    break;
                case "Process":
                    result = UnifiedBlockingReason.ProcessWait;
                    break;
                case "Unknown":
                    result = UnifiedBlockingReason.Unknown;
                    break;
                case "None":
                    result = UnifiedBlockingReason.None;
                    break;
                case "Timer":
                    result = UnifiedBlockingReason.Timer;
                    break;
                case "Event":
                    result = UnifiedBlockingReason.Event;
                    break;
                    //case "Callback": break;
                    //case "Desktop": break;
                    //case "Key": break;
                    //case "IoCompletion": break;
                    //case "Directory": break;
                    //case "WindowStation": break;
                    //case "WaitCompletionPacket": break;
                    //case "TpWorkerFactory": break;
                    //case "Timer": break;
            }
            return result;
        }

    }


    public enum UnifiedBlockingReason
    {
        //Based on ClrThread BlockingReason Enumerations
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

        //Based on WCT_OBJECT_TYPE Enumerations
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

        Event = 22,        //An object which encapsulates some information, to be used for notifying processes of something.
        Timer = 23,
        MemorySection = 24
    }
}
