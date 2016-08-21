using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DWORD = System.Int32;

namespace msos
{
    static class NativeStructs
    {
        #region DbgHelp

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct MINIDUMP_MODULE
        {
            public UInt64 BaseOfImage;
            public UInt32 SizeOfImage;
            public UInt32 CheckSum;
            public UInt32 TimeDateStamp;
            public Int32 ModuleNameRva;
            public VS_FIXEDFILEINFO VersionInfo;
            public MINIDUMP_LOCATION_DESCRIPTOR CvRecord;
            public MINIDUMP_LOCATION_DESCRIPTOR MiscRecord;
            public UInt64 Reserved0;
            public UInt64 Reserved1;
        }


        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct MINIDUMP_SYSTEM_INFO
        {
            public ushort ProcessorArchitecture;
            public ushort ProcessorLevel;
            public ushort ProcessorRevision;
            public byte NumberOfProcessors;
            public byte ProductType;
            public UInt32 MajorVersion;
            public UInt32 MinorVersion;
            public UInt32 BuildNumber;
            public UInt32 PlatformId;
            public uint CSDVersionRva;
            public ushort SuiteMask;
            public ushort Reserved2;
            public CPU_INFORMATION Cpu;
        }

        [StructLayout(LayoutKind.Explicit)]
        public unsafe struct CPU_INFORMATION
        {
            // OtherCpuInfo
            [FieldOffset(0)]
            public fixed ulong ProcessorFeatures[2];
            // X86CpuInfo, Official VendorId is 3 * 32bit long's (EAX, EBX and ECX).
            // It actually stores a 12 byte ASCII string though, so it's easier for us to treat it as a 12 byte array instead.
            [FieldOffset(0)]
            public fixed byte VendorId[12];
            [FieldOffset(12)]
            public UInt32 VersionInformation;
            [FieldOffset(16)]
            public UInt32 FeatureInformation;
            [FieldOffset(20)]
            public UInt32 AMDExtendedCpuFeatures;
        }

        public unsafe struct MINIDUMP_STRING
        {
            public UInt32 Length;
            public char* Buffer;
        }

        public struct MINIDUMP_HANDLE_DATA_STREAM
        {
            public UInt32 SizeOfHeader;
            public UInt32 SizeOfDescriptor;
            public UInt32 NumberOfDescriptors;
            public UInt32 Reserved;
        }

        public struct MINIDUMP_HANDLE_DESCRIPTOR_2
        {
            public UInt64 Handle;
            public Int32 TypeNameRva;
            public Int32 ObjectNameRva;
            public UInt32 Attributes;
            public UInt32 GrantedAccess;
            public UInt32 HandleCount;
            public UInt32 PointerCount;
            public Int32 ObjectInfoRva;
            public UInt32 Reserved0;
        }

        public struct MINIDUMP_HANDLE_OBJECT_INFORMATION
        {
            public uint NextInfoRva;
            public MINIDUMP_HANDLE_OBJECT_INFORMATION_TYPE InfoType;
            public UInt32 SizeOfInfo;
        }

        public struct MINIDUMP_HANDLE_DESCRIPTOR
        {
            public UInt64 Handle;
            public Int32 TypeNameRva;
            public Int32 ObjectNameRva;
            public UInt32 Attributes;
            public UInt32 GrantedAccess;
            public UInt32 HandleCount;
            public UInt32 PointerCount;
        }

        public struct MINIDUMP_DIRECTORY
        {
            public UInt32 StreamType;
            public MINIDUMP_LOCATION_DESCRIPTOR Location;
        }

        public struct MINIDUMP_LOCATION_DESCRIPTOR
        {
            public UInt32 DataSize;
            public uint Rva;
        }

        #endregion

        #region Kernel32

        public struct VS_FIXEDFILEINFO
        {
            public UInt32 dwSignature;
            public UInt32 dwStrucVersion;
            public UInt32 dwFileVersionMS;
            public UInt32 dwFileVersionLS;
            public UInt32 dwProductVersionMS;
            public UInt32 dwProductVersionLS;
            public UInt32 dwFileFlagsMask;
            public UInt32 dwFileFlags;
            public UInt32 dwFileOS;
            public UInt32 dwFileType;
            public UInt32 dwFileSubtype;
            public UInt32 dwFileDateMS;
            public UInt32 dwFileDateLS;
        };

        public struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public UIntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        #endregion

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

        public unsafe struct PUBLIC_OBJECT_TYPE_INFORMATION
        {
            public UNICODE_STRING TypeName;
            public ulong Reserved;
        };

        public struct OBJECT_NAME_INFORMATION
        {
            public UNICODE_STRING Name;
            //public ulong NameBuffer;
        };

        #endregion

        #region WinBase

        [StructLayout(LayoutKind.Sequential)]
        public struct CRITICAL_SECTION
        {
            public Int64 LockCount;
            public Int64 RecursionCount;
            public IntPtr OwningThread;        // from the thread's ClientId->UniqueThread
            public IntPtr LockSemaphore;
            public UIntPtr SpinCount;        // force size on 64-bit systems when packed
        };

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


#if !(_WIN64)

        public struct THREAD_ADDITIONAL_INFO
        {
            public DWORD Unknown1;
            public DWORD Unknown2;
            public DWORD ProcessId;
            public DWORD ThreadId;
            public DWORD Unknown3;
            public DWORD Priority;
            public DWORD Unknown4;
        };

        public struct MUTEX_ADDITIONAL_INFO_1
        {
            public DWORD Unknown1;
            public DWORD Unknown2;
        };

        public struct MUTEX_ADDITIONAL_INFO_2
        {
            public DWORD OwnerProcessId;
            public DWORD OwnerThreadId;
        };

        public struct PROCESS_ADDITIONAL_INFO_2
        {
            public DWORD Unknown1;
            public DWORD Unknown2;
            public DWORD Unknown3;
            public DWORD Unknown4;
            public DWORD Unknown5;
            public DWORD ProcessId;
            public DWORD ParentProcessId;
            public DWORD Unknown6;
        };

#else

    struct MUTEX_ADDITIONAL_INFO_2
    {
        DWORD OwnerProcessId;
        DWORD Unknown1;
        DWORD OwnerThreadId;
        DWORD Unknown2;
    };

    struct MUTEX_ADDITIONAL_INFO_1
    {
        DWORD Unknown1;
        DWORD Unknown2;
    };

    struct THREAD_ADDITIONAL_INFO
    {
        DWORD Unknown1;
        DWORD Unknown2;
        DWORD Unknown3;
        DWORD Unknown4;
        DWORD ProcessId;
        DWORD Unknown5;
        DWORD ThreadId;
        DWORD Unknown6;
        DWORD Unknown7;
        DWORD Unknown8;
        DWORD Priority;
        DWORD Unknown9;
    };

    struct PROCESS_ADDITIONAL_INFO_2
    {
        DWORD Unknown1;
        DWORD Unknown2;
        DWORD Unknown3;
        DWORD Unknown4;
        DWORD Unknown5;
        DWORD Unknown6;
        DWORD Unknown7;
        DWORD Unknown8;
        DWORD BasePriority;
        DWORD Unknown10;
        DWORD ProcessId;
        DWORD Unknown12;
        DWORD ParentProcessId;
        DWORD Unknown14;
        DWORD Unknown15;
        DWORD Unknown16;
    };

#endif

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

    /// <summary>
    /// Doc: https://msdn.microsoft.com/en-us/library/windows/desktop/ms681388(v=vs.85).aspx
    /// </summary>
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
    };

    public enum NtStatus : uint
    {
        Success = 0x00000000,
        Wait0 = 0x00000000,
        Wait1 = 0x00000001,
        Wait2 = 0x00000002,
        Wait3 = 0x00000003,
        Wait63 = 0x0000003f,
        Abandoned = 0x00000080,
        AbandonedWait0 = 0x00000080,
        AbandonedWait1 = 0x00000081,
        AbandonedWait2 = 0x00000082,
        AbandonedWait3 = 0x00000083,
        AbandonedWait63 = 0x000000bf,
        UserApc = 0x000000c0,
        KernelApc = 0x00000100,
        Alerted = 0x00000101,
        Timeout = 0x00000102,
        Pending = 0x00000103,
        Reparse = 0x00000104,
        MoreEntries = 0x00000105,
        NotAllAssigned = 0x00000106,
        SomeNotMapped = 0x00000107,
        OpLockBreakInProgress = 0x00000108,
        VolumeMounted = 0x00000109,
        RxActCommitted = 0x0000010a,
        NotifyCleanup = 0x0000010b,
        NotifyEnumDir = 0x0000010c,
        NoQuotasForAccount = 0x0000010d,
        PrimaryTransportConnectFailed = 0x0000010e,
        PageFaultTransition = 0x00000110,
        PageFaultDemandZero = 0x00000111,
        PageFaultCopyOnWrite = 0x00000112,
        PageFaultGuardPage = 0x00000113,
        PageFaultPagingFile = 0x00000114,
        CrashDump = 0x00000116,
        ReparseObject = 0x00000118,
        NothingToTerminate = 0x00000122,
        ProcessNotInJob = 0x00000123,
        ProcessInJob = 0x00000124,
        ProcessCloned = 0x00000129,
        FileLockedWithOnlyReaders = 0x0000012a,
        FileLockedWithWriters = 0x0000012b,

        // Informational
        Informational = 0x40000000,
        ObjectNameExists = 0x40000000,
        ThreadWasSuspended = 0x40000001,
        WorkingSetLimitRange = 0x40000002,
        ImageNotAtBase = 0x40000003,
        RegistryRecovered = 0x40000009,

        // Warning
        Warning = 0x80000000,
        GuardPageViolation = 0x80000001,
        DatatypeMisalignment = 0x80000002,
        Breakpoint = 0x80000003,
        SingleStep = 0x80000004,
        BufferOverflow = 0x80000005,
        NoMoreFiles = 0x80000006,
        HandlesClosed = 0x8000000a,
        PartialCopy = 0x8000000d,
        DeviceBusy = 0x80000011,
        InvalidEaName = 0x80000013,
        EaListInconsistent = 0x80000014,
        NoMoreEntries = 0x8000001a,
        LongJump = 0x80000026,
        DllMightBeInsecure = 0x8000002b,

        // Error
        Error = 0xc0000000,
        Unsuccessful = 0xc0000001,
        NotImplemented = 0xc0000002,
        InvalidInfoClass = 0xc0000003,
        InfoLengthMismatch = 0xc0000004,
        AccessViolation = 0xc0000005,
        InPageError = 0xc0000006,
        PagefileQuota = 0xc0000007,
        InvalidHandle = 0xc0000008,
        BadInitialStack = 0xc0000009,
        BadInitialPc = 0xc000000a,
        InvalidCid = 0xc000000b,
        TimerNotCanceled = 0xc000000c,
        InvalidParameter = 0xc000000d,
        NoSuchDevice = 0xc000000e,
        NoSuchFile = 0xc000000f,
        InvalidDeviceRequest = 0xc0000010,
        EndOfFile = 0xc0000011,
        WrongVolume = 0xc0000012,
        NoMediaInDevice = 0xc0000013,
        NoMemory = 0xc0000017,
        NotMappedView = 0xc0000019,
        UnableToFreeVm = 0xc000001a,
        UnableToDeleteSection = 0xc000001b,
        IllegalInstruction = 0xc000001d,
        AlreadyCommitted = 0xc0000021,
        AccessDenied = 0xc0000022,
        BufferTooSmall = 0xc0000023,
        ObjectTypeMismatch = 0xc0000024,
        NonContinuableException = 0xc0000025,
        BadStack = 0xc0000028,
        NotLocked = 0xc000002a,
        NotCommitted = 0xc000002d,
        InvalidParameterMix = 0xc0000030,
        ObjectNameInvalid = 0xc0000033,
        ObjectNameNotFound = 0xc0000034,
        ObjectNameCollision = 0xc0000035,
        ObjectPathInvalid = 0xc0000039,
        ObjectPathNotFound = 0xc000003a,
        ObjectPathSyntaxBad = 0xc000003b,
        DataOverrun = 0xc000003c,
        DataLate = 0xc000003d,
        DataError = 0xc000003e,
        CrcError = 0xc000003f,
        SectionTooBig = 0xc0000040,
        PortConnectionRefused = 0xc0000041,
        InvalidPortHandle = 0xc0000042,
        SharingViolation = 0xc0000043,
        QuotaExceeded = 0xc0000044,
        InvalidPageProtection = 0xc0000045,
        MutantNotOwned = 0xc0000046,
        SemaphoreLimitExceeded = 0xc0000047,
        PortAlreadySet = 0xc0000048,
        SectionNotImage = 0xc0000049,
        SuspendCountExceeded = 0xc000004a,
        ThreadIsTerminating = 0xc000004b,
        BadWorkingSetLimit = 0xc000004c,
        IncompatibleFileMap = 0xc000004d,
        SectionProtection = 0xc000004e,
        EasNotSupported = 0xc000004f,
        EaTooLarge = 0xc0000050,
        NonExistentEaEntry = 0xc0000051,
        NoEasOnFile = 0xc0000052,
        EaCorruptError = 0xc0000053,
        FileLockConflict = 0xc0000054,
        LockNotGranted = 0xc0000055,
        DeletePending = 0xc0000056,
        CtlFileNotSupported = 0xc0000057,
        UnknownRevision = 0xc0000058,
        RevisionMismatch = 0xc0000059,
        InvalidOwner = 0xc000005a,
        InvalidPrimaryGroup = 0xc000005b,
        NoImpersonationToken = 0xc000005c,
        CantDisableMandatory = 0xc000005d,
        NoLogonServers = 0xc000005e,
        NoSuchLogonSession = 0xc000005f,
        NoSuchPrivilege = 0xc0000060,
        PrivilegeNotHeld = 0xc0000061,
        InvalidAccountName = 0xc0000062,
        UserExists = 0xc0000063,
        NoSuchUser = 0xc0000064,
        GroupExists = 0xc0000065,
        NoSuchGroup = 0xc0000066,
        MemberInGroup = 0xc0000067,
        MemberNotInGroup = 0xc0000068,
        LastAdmin = 0xc0000069,
        WrongPassword = 0xc000006a,
        IllFormedPassword = 0xc000006b,
        PasswordRestriction = 0xc000006c,
        LogonFailure = 0xc000006d,
        AccountRestriction = 0xc000006e,
        InvalidLogonHours = 0xc000006f,
        InvalidWorkstation = 0xc0000070,
        PasswordExpired = 0xc0000071,
        AccountDisabled = 0xc0000072,
        NoneMapped = 0xc0000073,
        TooManyLuidsRequested = 0xc0000074,
        LuidsExhausted = 0xc0000075,
        InvalidSubAuthority = 0xc0000076,
        InvalidAcl = 0xc0000077,
        InvalidSid = 0xc0000078,
        InvalidSecurityDescr = 0xc0000079,
        ProcedureNotFound = 0xc000007a,
        InvalidImageFormat = 0xc000007b,
        NoToken = 0xc000007c,
        BadInheritanceAcl = 0xc000007d,
        RangeNotLocked = 0xc000007e,
        DiskFull = 0xc000007f,
        ServerDisabled = 0xc0000080,
        ServerNotDisabled = 0xc0000081,
        TooManyGuidsRequested = 0xc0000082,
        GuidsExhausted = 0xc0000083,
        InvalidIdAuthority = 0xc0000084,
        AgentsExhausted = 0xc0000085,
        InvalidVolumeLabel = 0xc0000086,
        SectionNotExtended = 0xc0000087,
        NotMappedData = 0xc0000088,
        ResourceDataNotFound = 0xc0000089,
        ResourceTypeNotFound = 0xc000008a,
        ResourceNameNotFound = 0xc000008b,
        ArrayBoundsExceeded = 0xc000008c,
        FloatDenormalOperand = 0xc000008d,
        FloatDivideByZero = 0xc000008e,
        FloatInexactResult = 0xc000008f,
        FloatInvalidOperation = 0xc0000090,
        FloatOverflow = 0xc0000091,
        FloatStackCheck = 0xc0000092,
        FloatUnderflow = 0xc0000093,
        IntegerDivideByZero = 0xc0000094,
        IntegerOverflow = 0xc0000095,
        PrivilegedInstruction = 0xc0000096,
        TooManyPagingFiles = 0xc0000097,
        FileInvalid = 0xc0000098,
        InstanceNotAvailable = 0xc00000ab,
        PipeNotAvailable = 0xc00000ac,
        InvalidPipeState = 0xc00000ad,
        PipeBusy = 0xc00000ae,
        IllegalFunction = 0xc00000af,
        PipeDisconnected = 0xc00000b0,
        PipeClosing = 0xc00000b1,
        PipeConnected = 0xc00000b2,
        PipeListening = 0xc00000b3,
        InvalidReadMode = 0xc00000b4,
        IoTimeout = 0xc00000b5,
        FileForcedClosed = 0xc00000b6,
        ProfilingNotStarted = 0xc00000b7,
        ProfilingNotStopped = 0xc00000b8,
        NotSameDevice = 0xc00000d4,
        FileRenamed = 0xc00000d5,
        CantWait = 0xc00000d8,
        PipeEmpty = 0xc00000d9,
        CantTerminateSelf = 0xc00000db,
        publicError = 0xc00000e5,
        InvalidParameter1 = 0xc00000ef,
        InvalidParameter2 = 0xc00000f0,
        InvalidParameter3 = 0xc00000f1,
        InvalidParameter4 = 0xc00000f2,
        InvalidParameter5 = 0xc00000f3,
        InvalidParameter6 = 0xc00000f4,
        InvalidParameter7 = 0xc00000f5,
        InvalidParameter8 = 0xc00000f6,
        InvalidParameter9 = 0xc00000f7,
        InvalidParameter10 = 0xc00000f8,
        InvalidParameter11 = 0xc00000f9,
        InvalidParameter12 = 0xc00000fa,
        MappedFileSizeZero = 0xc000011e,
        TooManyOpenedFiles = 0xc000011f,
        Cancelled = 0xc0000120,
        CannotDelete = 0xc0000121,
        InvalidComputerName = 0xc0000122,
        FileDeleted = 0xc0000123,
        SpecialAccount = 0xc0000124,
        SpecialGroup = 0xc0000125,
        SpecialUser = 0xc0000126,
        MembersPrimaryGroup = 0xc0000127,
        FileClosed = 0xc0000128,
        TooManyThreads = 0xc0000129,
        ThreadNotInProcess = 0xc000012a,
        TokenAlreadyInUse = 0xc000012b,
        PagefileQuotaExceeded = 0xc000012c,
        CommitmentLimit = 0xc000012d,
        InvalidImageLeFormat = 0xc000012e,
        InvalidImageNotMz = 0xc000012f,
        InvalidImageProtect = 0xc0000130,
        InvalidImageWin16 = 0xc0000131,
        LogonServer = 0xc0000132,
        DifferenceAtDc = 0xc0000133,
        SynchronizationRequired = 0xc0000134,
        DllNotFound = 0xc0000135,
        IoPrivilegeFailed = 0xc0000137,
        OrdinalNotFound = 0xc0000138,
        EntryPointNotFound = 0xc0000139,
        ControlCExit = 0xc000013a,
        PortNotSet = 0xc0000353,
        DebuggerInactive = 0xc0000354,
        CallbackBypass = 0xc0000503,
        PortClosed = 0xc0000700,
        MessageLost = 0xc0000701,
        InvalidMessage = 0xc0000702,
        RequestCanceled = 0xc0000703,
        RecursiveDispatch = 0xc0000704,
        LpcReceiveBufferExpected = 0xc0000705,
        LpcInvalidConnectionUsage = 0xc0000706,
        LpcRequestsNotAllowed = 0xc0000707,
        ResourceInUse = 0xc0000708,
        ProcessIsProtected = 0xc0000712,
        VolumeDirty = 0xc0000806,
        FileCheckedOut = 0xc0000901,
        CheckOutRequired = 0xc0000902,
        BadFileType = 0xc0000903,
        FileTooLarge = 0xc0000904,
        FormsAuthRequired = 0xc0000905,
        VirusInfected = 0xc0000906,
        VirusDeleted = 0xc0000907,
        TransactionalConflict = 0xc0190001,
        InvalidTransaction = 0xc0190002,
        TransactionNotActive = 0xc0190003,
        TmInitializationFailed = 0xc0190004,
        RmNotActive = 0xc0190005,
        RmMetadataCorrupt = 0xc0190006,
        TransactionNotJoined = 0xc0190007,
        DirectoryNotRm = 0xc0190008,
        CouldNotResizeLog = 0xc0190009,
        TransactionsUnsupportedRemote = 0xc019000a,
        LogResizeInvalidSize = 0xc019000b,
        RemoteFileVersionMismatch = 0xc019000c,
        CrmProtocolAlreadyExists = 0xc019000f,
        TransactionPropagationFailed = 0xc0190010,
        CrmProtocolNotFound = 0xc0190011,
        TransactionSuperiorExists = 0xc0190012,
        TransactionRequestNotValid = 0xc0190013,
        TransactionNotRequested = 0xc0190014,
        TransactionAlreadyAborted = 0xc0190015,
        TransactionAlreadyCommitted = 0xc0190016,
        TransactionInvalidMarshallBuffer = 0xc0190017,
        CurrentTransactionNotValid = 0xc0190018,
        LogGrowthFailed = 0xc0190019,
        ObjectNoLongerExists = 0xc0190021,
        StreamMiniversionNotFound = 0xc0190022,
        StreamMiniversionNotValid = 0xc0190023,
        MiniversionInaccessibleFromSpecifiedTransaction = 0xc0190024,
        CantOpenMiniversionWithModifyIntent = 0xc0190025,
        CantCreateMoreStreamMiniversions = 0xc0190026,
        HandleNoLongerValid = 0xc0190028,
        NoTxfMetadata = 0xc0190029,
        LogCorruptionDetected = 0xc0190030,
        CantRecoverWithHandleOpen = 0xc0190031,
        RmDisconnected = 0xc0190032,
        EnlistmentNotSuperior = 0xc0190033,
        RecoveryNotNeeded = 0xc0190034,
        RmAlreadyStarted = 0xc0190035,
        FileIdentityNotPersistent = 0xc0190036,
        CantBreakTransactionalDependency = 0xc0190037,
        CantCrossRmBoundary = 0xc0190038,
        TxfDirNotEmpty = 0xc0190039,
        IndoubtTransactionsExist = 0xc019003a,
        TmVolatile = 0xc019003b,
        RollbackTimerExpired = 0xc019003c,
        TxfAttributeCorrupt = 0xc019003d,
        EfsNotAllowedInTransaction = 0xc019003e,
        TransactionalOpenNotAllowed = 0xc019003f,
        TransactedMappingUnsupportedRemote = 0xc0190040,
        TxfMetadataAlreadyPresent = 0xc0190041,
        TransactionScopeCallbacksNotSet = 0xc0190042,
        TransactionRequiredPromotion = 0xc0190043,
        CannotExecuteFileInTransaction = 0xc0190044,
        TransactionsNotFrozen = 0xc0190045,

        MaximumNtStatus = 0xffffffff
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
    public enum FileMapAccess : uint
    {
        FileMapCopy = 0x0001,
        FileMapWrite = 0x0002,
        FileMapRead = 0x0004,
        FileMapAllAccess = 0x001f,
        FileMapExecute = 0x0020,
    }

    [Flags]
    public enum DuplicateOptions : uint
    {
        DUPLICATE_CLOSE_SOURCE = (0x00000001),// Closes the source handle. This occurs regardless of any error status returned.
        DUPLICATE_SAME_ACCESS = (0x00000002), //Ignores the dwDesiredAccess parameter. The duplicate handle has the same access as the source handle.
    }

    #endregion

    #region DbgHelp

    public enum MINIDUMP_STREAM_TYPE : uint
    {
        UnusedStream = 0,
        ReservedStream0 = 1,
        ReservedStream1 = 2,
        ThreadListStream = 3,
        ModuleListStream = 4,
        MemoryListStream = 5,
        ExceptionStream = 6,
        SystemInfoStream = 7,
        ThreadExListStream = 8,
        Memory64ListStream = 9,
        CommentStreamA = 10,
        CommentStreamW = 11,
        HandleDataStream = 12,
        FunctionTableStream = 13,
        UnloadedModuleListStream = 14,
        MiscInfoStream = 15,
        MemoryInfoListStream = 16,
        ThreadInfoListStream = 17,
        HandleOperationListStream = 18,
        LastReservedStream = 0xffff
    }

    public enum MiniDumpProcessorArchitecture
    {
        PROCESSOR_ARCHITECTURE_INTEL = 0,
        PROCESSOR_ARCHITECTURE_IA64 = 6,
        PROCESSOR_ARCHITECTURE_AMD64 = 9,
        PROCESSOR_ARCHITECTURE_UNKNOWN = 0xfff
    }

    public enum MiniDumpProductType
    {
        VER_NT_WORKSTATION = 0x0000001,
        VER_NT_DOMAIN_CONTROLLER = 0x0000002,
        VER_NT_SERVER = 0x0000003
    }

    public enum MiniDumpPlatform
    {
        VER_PLATFORM_WIN32s = 0,
        VER_PLATFORM_WIN32_WINDOWS = 1,
        VER_PLATFORM_WIN32_NT = 2
    }

    // Per-handle object information varies according to
    // the OS, the OS version, the processor type and
    // so on.  The minidump gives a minidump identifier
    // to each possible data format for identification
    // purposes but does not control nor describe the actual data.
    public enum MINIDUMP_HANDLE_OBJECT_INFORMATION_TYPE : uint
    {
        MiniHandleObjectInformationNone,
        MiniThreadInformation1,
        MiniMutantInformation1,
        MiniMutantInformation2,
        MiniProcessInformation1,
        MiniProcessInformation2,
        MiniEventInformation1,
        MiniSectionInformation1,
        MiniHandleObjectInformationTypeMax
    }

    #endregion

    #region WinNt

    [StructLayout(LayoutKind.Sequential)]
    public struct CONTEXT_X86
    {
        public uint ContextFlags; //set this to an appropriate value 
                                  // Retrieved by CONTEXT_DEBUG_REGISTERS 
        public uint Dr0;
        public uint Dr1;
        public uint Dr2;
        public uint Dr3;
        public uint Dr6;
        public uint Dr7;
        // Retrieved by CONTEXT_FLOATING_POINT 
        public FLOATING_SAVE_AREA FloatSave;
        // Retrieved by CONTEXT_SEGMENTS 
        public uint SegGs;
        public uint SegFs;
        public uint SegEs;
        public uint SegDs;
        // Retrieved by CONTEXT_INTEGER 
        public uint Edi;
        public uint Esi;
        public uint Ebx;
        public uint Edx;
        public uint Ecx;
        public uint Eax;
        // Retrieved by CONTEXT_CONTROL 
        public uint Ebp;
        public uint Eip;
        public uint SegCs;
        public uint EFlags;
        public uint Esp;
        public uint SegSs;
        // Retrieved by CONTEXT_EXTENDED_REGISTERS 
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
        public byte[] ExtendedRegisters;
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct FLOATING_SAVE_AREA
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


    #endregion
}
