using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    class TimeAndMemory : IDisposable
    {
        private Stopwatch _sw;
        private long _heapSizeStart;
        private bool _diagnosticModeOn;

        private static void DoFullCollect()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        }

        public TimeAndMemory(bool diagnosticModeOn)
        {
            _diagnosticModeOn = diagnosticModeOn;
            if (!_diagnosticModeOn)
                return;

            _sw = Stopwatch.StartNew();
            DoFullCollect();
            _heapSizeStart = GC.GetTotalMemory(false);
        }

        public void Dispose()
        {
            if (!_diagnosticModeOn)
                return;

            _sw.Stop();
            DoFullCollect();
            long heapSizeEnd = GC.GetTotalMemory(false);
            ConsolePrinter.WriteInfo("Time: {0} ms, Memory start: {1}, Memory end: {2}, Memory delta: {3}{4}",
                _sw.ElapsedMilliseconds,
                ((ulong)_heapSizeStart).ToMemoryUnits(),
                ((ulong)heapSizeEnd).ToMemoryUnits(), 
                heapSizeEnd > _heapSizeStart ? "+" : "-",
                ((ulong)Math.Abs(heapSizeEnd - _heapSizeStart)).ToMemoryUnits());
        }
    }
}
