using System;
using System.Runtime.InteropServices;

namespace msos
{
    static class NativeStructs
    {
        #region Advapi32.dll

        [StructLayout(LayoutKind.Sequential)]
        public struct WAITCHAIN_NODE_INFO
        {
            public WCT_OBJECT_TYPE ObjectType;
            public WCT_OBJECT_STATUS ObjectStatus;
            public _WAITCHAIN_NODE_INFO_UNION Union;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct _WAITCHAIN_NODE_INFO_UNION
        {
            [FieldOffset(0)]
            public _WAITCHAIN_NODE_INFO_LOCK_OBJECT LockObject;
            [FieldOffset(0)]
            public _WAITCHAIN_NODE_INFO_THREAD_OBJECT ThreadObject;
        }

        public unsafe struct _WAITCHAIN_NODE_INFO_LOCK_OBJECT
        {
            /*The name of the object. Object names are only available for certain object, such as mutexes. If the object does not have a name, this member is an empty string.*/
            public fixed byte ObjectName[128 * 2];
            /*This member is reserved for future use.*/
            public UInt64 Timeout;
            /*This member is reserved for future use.*/
            public UInt32 Alertable;
        }

        public struct _WAITCHAIN_NODE_INFO_THREAD_OBJECT
        {
            /*The process identifier.*/
            public UInt32 ProcessId;
            /*The thread identifier. For COM and ALPC, this member can be 0.*/
            public UInt32 ThreadId;
            /*The wait time.*/
            public UInt32 WaitTime;
            /*The number of context switches.*/
            public UInt32 ContextSwitches;

        }

        #endregion

        #region NtDll

        [StructLayout(LayoutKind.Explicit, Size = 8)]

        public unsafe struct PUBLIC_OBJECT_TYPE_INFORMATION
        {
            public UNICODE_STRING TypeName;
            public fixed uint Reserved[22];
        }

        public struct OBJECT_NAME_INFORMATION
        {
            public UNICODE_STRING Name;
        }

        #endregion

        #region WinBase

        [StructLayout(LayoutKind.Sequential)]
        public struct CRITICAL_SECTION
        {
            public IntPtr DebugInfo;
            public int LockCount;
            public int RecursionCount;
            public IntPtr OwningThread;
            public IntPtr LockSemaphore;
            public UIntPtr SpinCount;
        }

        #endregion

        #region Other

        [StructLayout(LayoutKind.Sequential)]
        public struct UNICODE_STRING : IDisposable
        {
            public ushort Length;
            public ushort MaximumLength;
            private IntPtr buffer;

            public UNICODE_STRING(string s)
            {
                Length = (ushort)(s.Length * 2);
                MaximumLength = (ushort)(Length + 2);
                buffer = Marshal.StringToHGlobalUni(s);
            }

            public void Dispose()
            {
                Marshal.FreeHGlobal(buffer);
                buffer = IntPtr.Zero;
            }

            public override string ToString()
            {
                return Marshal.PtrToStringUni(buffer);
            }
        }

        #endregion
    }

    #region Advapi32.dll Enums

    public enum WCT_OBJECT_TYPE
    {
        WctCriticalSectionType = 1,
        WctSendMessageType = 2,
        WctMutexType = 3,
        WctAlpcType = 4,
        WctComType = 5,
        WctThreadWaitType = 6,
        WctProcessWaitType = 7,
        WctThreadType = 8,
        WctComActivationType = 9,
        WctUnknownType = 10,
        WctMaxType = 11,
    }

    public enum WCT_OBJECT_STATUS
    {
        WctStatusNoAccess = 1,    // ACCESS_DENIED for this object 
        WctStatusRunning = 2,     // Thread status 
        WctStatusBlocked = 3,     // Thread status 
        WctStatusPidOnly = 4,     // Thread status 
        WctStatusPidOnlyRpcss = 5,// Thread status 
        WctStatusOwned = 6,       // Dispatcher object status 
        WctStatusNotOwned = 7,    // Dispatcher object status 
        WctStatusAbandoned = 8,   // Dispatcher object status 
        WctStatusUnknown = 9,     // All objects 
        WctStatusError = 10,      // All objects 
        WctStatusMax = 11
    }

    public enum SYSTEM_ERROR_CODES
    {
        /// <summary>
        /// Overlapped I/O operation is in progress. (997 (0x3E5))
        /// </summary>
        ERROR_IO_PENDING = 997
    }

    public enum WCT_SESSION_OPEN_FLAGS
    {
        WCT_SYNC_OPEN_FLAG = 0,
        WCT_ASYNC_OPEN_FLAG = 1
    }

    #endregion

    #region NtDll Enums


    public enum OBJECT_INFORMATION_CLASS
    {
        ObjectBasicInformation,
        ObjectNameInformation,
        ObjectTypeInformation,
        ObjectAllInformation,
        ObjectDataInformation
    }

    public enum NtStatus : uint
    {
        Success = 0x00000000,
        InvalidHandle = 0xc0000008
    }

    #endregion

    #region Kernel32

    [Flags]
    public enum ProcessAccessFlags : uint
    {
        All = 0x001F0FFF,
        Terminate = 0x00000001,
        CreateThread = 0x00000002,
        VirtualMemoryOperation = 0x00000008,
        VirtualMemoryRead = 0x00000010,
        VirtualMemoryWrite = 0x00000020,
        DuplicateHandle = 0x00000040,
        CreateProcess = 0x000000080,
        SetQuota = 0x00000100,
        SetInformation = 0x00000200,
        QueryInformation = 0x00000400,
        QueryLimitedInformation = 0x00001000,
        Synchronize = 0x00100000
    }

    [Flags]
    public enum DuplicateOptions : uint
    {
        DUPLICATE_CLOSE_SOURCE = (0x00000001),// Closes the source handle. This occurs regardless of any error status returned.
        DUPLICATE_SAME_ACCESS = (0x00000002), //Ignores the dwDesiredAccess parameter. The duplicate handle has the same access as the source handle.
    }

    #endregion

    #region Context

#if X86

    [StructLayout(LayoutKind.Sequential)]
    struct FLOATING_SAVE_AREA
    {
        public uint ControlWord;
        public uint StatusWord;
        public uint TagWord;
        public uint ErrorOffset;
        public uint ErrorSelector;
        public uint DataOffset;
        public uint DataSelector;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 80)]
        public byte[] RegisterArea;
        public uint Cr0NpxState;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct CONTEXT
    {
        public uint ContextFlags;
        public uint Dr0;
        public uint Dr1;
        public uint Dr2;
        public uint Dr3;
        public uint Dr6;
        public uint Dr7;
        public FLOATING_SAVE_AREA FloatSave;
        public uint SegGs;
        public uint SegFs;
        public uint SegEs;
        public uint SegDs;
        public uint Edi;
        public uint Esi;
        public uint Ebx;
        public uint Edx;
        public uint Ecx;
        public uint Eax;
        public uint Ebp;
        public uint Eip;
        public uint SegCs;
        public uint EFlags;
        public uint Esp;
        public uint SegSs;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
        public byte[] ExtendedRegisters;
    }
#elif X64

#pragma warning disable CS0169
    struct M128A
    {
        ulong Low;
        ulong High;
    }
#pragma warning restore CS0169

    struct XSAVE_FORMAT
    {
        public ushort ControlWord;
        public ushort StatusWord;
        public byte TagWord;
        public byte Reserved1;
        public ushort ErrorOpcode;
        public uint ErrorOffset;
        public ushort ErrorSelector;
        public ushort Reserved2;
        public uint DataOffset;
        public ushort DataSelector;
        public ushort Reserved3;
        public uint MxCsr;
        public uint MxCsr_Mask;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public M128A[] FloatRegisters;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public M128A[] XmmRegisters;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 96)]
        public byte[] Reserved4;
    }

    struct CONTEXT
    {
        public ulong P1Home;
        public ulong P2Home;
        public ulong P3Home;
        public ulong P4Home;
        public ulong P5Home;
        public ulong P6Home;

        public uint ContextFlags;
        public uint MxCsr;

        public ushort SegCs;
        public ushort SegDs;
        public ushort SegEs;
        public ushort SegSs;
        public ushort SegFs;
        public ushort SegGs;
        public uint EFlags;

        public ulong Dr0;
        public ulong Dr1;
        public ulong Dr2;
        public ulong Dr3;
        public ulong Dr6;
        public ulong Dr7;

        public ulong Rax;
        public ulong Rcx;
        public ulong Rdx;
        public ulong Rbx;
        public ulong Rsp;
        public ulong Rbp;
        public ulong Rsi;
        public ulong Rdi;
        public ulong R8;
        public ulong R9;
        public ulong R10;
        public ulong R11;
        public ulong R12;
        public ulong R13;
        public ulong R14;
        public ulong R15;

        public ulong Rip;

        public XSAVE_FORMAT FltSave;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 26)]
        public M128A[] VectorRegister;

        public ulong VectorControl;

        public ulong DebugControl;
        public ulong LastBranchToRip;
        public ulong LastBranchFromRip;
        public ulong LastExceptionToRip;
        public ulong LastExceptionFromRip;
    }
#endif

#endregion

}
