using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using FileTime = System.Runtime.InteropServices.ComTypes.FILETIME;
using static msos.NativeStructs;

namespace msos
{
    static class NativeMethods
    {
        #region Kernel32.dll

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWow64Process([In] IntPtr process, [Out] out bool wow64Process);

        [DllImport("kernel32.dll")]
        internal static extern bool GetProcessTimes(IntPtr hProcess, out FileTime lpCreationTime,
            out FileTime lpExitTime, out FileTime lpKernelTime, out FileTime lpUserTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr VirtualQuery(SafeMemoryMappedViewHandle address, ref MEMORY_BASIC_INFORMATION buffer, IntPtr sizeOfBuffer);


        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern SafeMemoryMappedViewHandle MapViewOfFile(
            SafeMemoryMappedFileHandle hFileMappingObject,
            FileMapAccess dwDesiredAccess,
            uint dwFileOffsetHigh,
            uint dwFileOffsetLow,
            IntPtr dwNumberOfBytesToMap);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DuplicateHandle(IntPtr hSourceProcessHandle,
           IntPtr hSourceHandle, IntPtr hTargetProcessHandle, out IntPtr lpTargetHandle,
           uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, DuplicateOptions options);

        #endregion

        #region Advapi32.dll

        [DllImport("Advapi32.dll", SetLastError = true)]
        internal static extern void CloseThreadWaitChainSession(IntPtr WctIntPtr);

        [DllImport("Advapi32.dll", SetLastError = true)]
        internal static extern IntPtr OpenThreadWaitChainSession(UInt32 Flags, UInt32 callback);

        [DllImport("Advapi32.dll", SetLastError = true)]
        internal static extern bool GetThreadWaitChain(
            IntPtr WctIntPtr,
            IntPtr Context,
            UInt32 Flags,
            uint ThreadId,
            ref int NodeCount,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)]
            [In, Out]
            WAITCHAIN_NODE_INFO[] NodeInfoArray,
            out int IsCycle
        );

        #endregion

        #region NtDll

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern NtStatus NtQueryObject(
              [In] IntPtr Handle,
              [In] OBJECT_INFORMATION_CLASS ObjectInformationClass,
              [Out] IntPtr ObjectInformation,
              [In] int ObjectInformationLength,
              [Out] out int ReturnLength);


        #endregion
    }

    static class Const
    {
        public const uint VS_FF_DEBUG = 0x00000001;
        public const uint VS_FF_INFOINFERRED = 0x00000010;
        public const uint VS_FF_PATCHED = 0x00000004;
        public const uint VS_FF_PRERELEASE = 0x00000002;
        public const uint VS_FF_PRIVATEBUILD = 0x00000008;
        public const uint VS_FF_SPECIALBUILD = 0x00000020;

        public const uint VOS_DOS = 0x00010000;
        public const uint VOS_NT = 0x00040000;
        public const uint VOS__WINDOWS16 = 0x00000001;
        public const uint VOS__WINDOWS32 = 0x00000004;
        public const uint VOS_OS216 = 0x00020000;
        public const uint VOS_OS232 = 0x00030000;
        public const uint VOS__PM16 = 0x00000002;
        public const uint VOS__PM32 = 0x00000003;
        public const uint VOS_UNKNOWN = 0x00000000;

        public const uint VOS_DOS_WINDOWS16 = 0x00010001;
        public const uint VOS_DOS_WINDOWS32 = 0x00010004;
        public const uint VOS_NT_WINDOWS32 = 0x00040004;
        public const uint VOS_OS216_PM16 = 0x00020002;
        public const uint VOS_OS232_PM32 = 0x00030003;

        public const uint VFT_APP = 0x00000001;
        public const uint VFT_DLL = 0x00000002;
        public const uint VFT_DRV = 0x00000003;
        public const uint VFT_FONT = 0x00000004;
        public const uint VFT_STATIC_LIB = 0x00000007;
        public const uint VFT_UNKNOWN = 0x00000000;
        public const uint VFT_VXD = 0x00000005;

        public const uint VFT2_DRV_COMM = 0x0000000A;
        public const uint VFT2_DRV_DISPLAY = 0x00000004;
        public const uint VFT2_DRV_INSTALLABLE = 0x00000008;
        public const uint VFT2_DRV_KEYBOARD = 0x00000002;
        public const uint VFT2_DRV_LANGUAGE = 0x00000003;
        public const uint VFT2_DRV_MOUSE = 0x00000005;
        public const uint VFT2_DRV_NETWORK = 0x00000006;
        public const uint VFT2_DRV_PRINTER = 0x00000001;
        public const uint VFT2_DRV_SOUND = 0x00000009;
        public const uint VFT2_DRV_SYSTEM = 0x00000007;
        public const uint VFT2_DRV_VERSIONED_PRINTER = 0x0000000C;
        public const uint VFT2_UNKNOWN = 0x00000000;

        public const uint VFT2_FONT_RASTER = 0x00000001;
        public const uint VFT2_FONT_TRUETYPE = 0x00000003;
        public const uint VFT2_FONT_VECTOR = 0x00000002;

        // Used by MINIDUMP_SYSTEM_INFO.SuiteMask
        public const ushort VER_SUITE_SMALLBUSINESS = 0x00000001;
        public const ushort VER_SUITE_ENTERPRISE = 0x00000002;
        public const ushort VER_SUITE_BACKOFFICE = 0x00000004;
        public const ushort VER_SUITE_COMMUNICATIONS = 0x00000008;
        public const ushort VER_SUITE_TERMINAL = 0x00000010;
        public const ushort VER_SUITE_SMALLBUSINESS_RESTRICTED = 0x00000020;
        public const ushort VER_SUITE_EMBEDDEDNT = 0x00000040;
        public const ushort VER_SUITE_DATACENTER = 0x00000080;
        public const ushort VER_SUITE_SINGLEUSERTS = 0x00000100;
        public const ushort VER_SUITE_PERSONAL = 0x00000200;
        public const ushort VER_SUITE_BLADE = 0x00000400;
        public const ushort VER_SUITE_EMBEDDED_RESTRICTED = 0x00000800;
        public const ushort VER_SUITE_SECURITY_APPLIANCE = 0x00001000;
        public const ushort VER_SUITE_STORAGE_SERVER = 0x00002000;
        public const ushort VER_SUITE_COMPUTE_SERVER = 0x00004000;
        public const ushort VER_SUITE_WH_SERVER = 0x00008000;

        public const ushort STILL_ACTIVE = 259;

        public const ushort EXCEPTION_MAXIMUM_PARAMETERS = 15; // maximum number of exception parameters

        public const ushort MAX_PATH = 260;
    }
}
