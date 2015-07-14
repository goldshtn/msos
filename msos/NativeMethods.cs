using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    using FileTime = System.Runtime.InteropServices.ComTypes.FILETIME;

    static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWow64Process([In] IntPtr process, [Out] out bool wow64Process);

        [DllImport("kernel32.dll")]
        internal static extern bool GetProcessTimes(IntPtr hProcess, out FileTime lpCreationTime,
            out FileTime lpExitTime, out FileTime lpKernelTime, out FileTime lpUserTime);
    }
}
