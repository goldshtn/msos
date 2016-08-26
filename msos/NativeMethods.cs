using System;
using System.Runtime.InteropServices;
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
}
