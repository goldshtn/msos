using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    using FileTime = System.Runtime.InteropServices.ComTypes.FILETIME;

    class TimeAndMemory : IDisposable
    {
        private Stopwatch _sw;
        private long _heapSizeStart;
        private bool _diagnosticModeOn;
        private IPrinter _printer;
        private ulong _cpuTimeBegin;

        private static void DoFullCollect()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        }

        public TimeAndMemory(bool diagnosticModeOn, IPrinter printer)
        {
            _diagnosticModeOn = diagnosticModeOn;
            _printer = printer;
            if (!_diagnosticModeOn)
                return;

            _cpuTimeBegin = GetCpuTimeTicks();
            _sw = Stopwatch.StartNew();
            DoFullCollect();
            _heapSizeStart = GC.GetTotalMemory(false);
        }

        public void Dispose()
        {
            if (!_diagnosticModeOn)
                return;

            ulong cpuTimeEnd = GetCpuTimeTicks();
            _sw.Stop();

            DoFullCollect();
            long heapSizeEnd = GC.GetTotalMemory(false);

            var wallClockElapsed = _sw.Elapsed.TotalMilliseconds;
            var cpuElapsed = new TimeSpan((long)(cpuTimeEnd - _cpuTimeBegin)).TotalMilliseconds;

            _printer.WriteInfo(
                "Time: {0:N} ms, CPU time: {1:N} ms ({2:N}%)" + Environment.NewLine +
                "Memory start: {3}, Memory end: {4}, Memory delta: {5}{6}",
                wallClockElapsed, cpuElapsed, 100.0*cpuElapsed/wallClockElapsed,
                ((ulong)_heapSizeStart).ToMemoryUnits(),
                ((ulong)heapSizeEnd).ToMemoryUnits(), 
                heapSizeEnd > _heapSizeStart ? "+" : "-",
                ((ulong)Math.Abs(heapSizeEnd - _heapSizeStart)).ToMemoryUnits());
        }

        private static ulong GetCpuTimeTicks()
        {
            FileTime creationTime, exitTime, kernelTime, userTime;
            if (!GetProcessTimes((IntPtr)(-1), out creationTime, out exitTime, out kernelTime, out userTime))
                return 0;

            return (((ulong)userTime.dwHighDateTime) << 32) + (ulong)userTime.dwLowDateTime;
        }

        [DllImport("kernel32.dll")]
        private static extern bool GetProcessTimes(IntPtr hProcess, out FileTime lpCreationTime,
            out FileTime lpExitTime, out FileTime lpKernelTime, out FileTime lpUserTime);
    }
}
