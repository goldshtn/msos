using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    class LastEventInformation
    {
        public int OSThreadId { get; set; }
        public string EventDescription { get; set; }
        public DEBUG_EVENT EventType { get; set; }
        public EXCEPTION_RECORD64? ExceptionRecord { get; set; }
    }

    static class DataTargetExtensions
    {
        public static LastEventInformation GetLastEventInformation(this DataTarget target)
        {
            var control = (IDebugControl)target.DebuggerInterface;
            DEBUG_EVENT eventType;
            uint procId, threadId;
            StringBuilder description = new StringBuilder(2048);
            uint unused;
            uint descriptionSize;
            if (0 != control.GetLastEventInformation(
                out eventType, out procId, out threadId,
                IntPtr.Zero, 0, out unused,
                description, description.Capacity, out descriptionSize))
            {
                return null;
            }

            var osThreadIds = target.GetOSThreadIds();
            var eventInformation = new LastEventInformation
            {
                OSThreadId = (int)osThreadIds[threadId],
                EventType = eventType,
                EventDescription = description.ToString()
            };

            IDebugAdvanced2 debugAdvanced = (IDebugAdvanced2)target.DebuggerInterface;
            int outSize;
            byte[] buffer = new byte[Marshal.SizeOf(typeof(EXCEPTION_RECORD64))];
            int hr = debugAdvanced.Request(DEBUG_REQUEST.TARGET_EXCEPTION_RECORD, null, 0, buffer, buffer.Length, out outSize);
            if (hr == 0)
            {
                GCHandle gch = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    eventInformation.ExceptionRecord = (EXCEPTION_RECORD64)Marshal.PtrToStructure(gch.AddrOfPinnedObject(), typeof(EXCEPTION_RECORD64));
                }
                finally
                {
                    gch.Free();
                }
            }

            return eventInformation;
        }

        public static IEnumerable<MEMORY_BASIC_INFORMATION64> EnumerateVMRegions(this DataTarget target)
        {
            var dataSpaces = (IDebugDataSpaces4)target.DebuggerInterface;
            ulong maxAddress = Environment.Is64BitProcess ? ulong.MaxValue : uint.MaxValue;
            ulong address = 0;
            while (true)
            {
                MEMORY_BASIC_INFORMATION64 memInfo;
                if (0 != dataSpaces.QueryVirtual(address, out memInfo))
                    break;

                // TODO 32-bit processes on 64-bit Windows with `/LARGEADDRESSAWARE`
                // enabled are behaving oddly here. Specifically, no valid addresses
                // above 2GB and below 4GB are reported by `QueryVirtual`, whereas the
                // WinDbg `!address` command can see them.
                if (memInfo.BaseAddress >= maxAddress || memInfo.RegionSize == 0)
                    break;

                yield return memInfo;

                address = memInfo.BaseAddress + memInfo.RegionSize;
            }
        }

        public static uint[] GetOSThreadIds(this DataTarget target)
        {
            var systemObjects = (IDebugSystemObjects3)target.DebuggerInterface;
            uint numThreads;
            if (0 != systemObjects.GetNumberThreads(out numThreads))
                return null;

            uint[] osThreadIds = new uint[numThreads];
            if (0 != systemObjects.GetThreadIdsByIndex(0, numThreads, null, osThreadIds))
                return null;

            return osThreadIds;
        }

        public static void ExecuteDbgEngCommand(this DataTarget target, string command, CommandExecutionContext context)
        {
            IDebugControl6 control = (IDebugControl6)target.DebuggerInterface;
            int hr = control.ExecuteWide(
                DEBUG_OUTCTL.THIS_CLIENT, command, DEBUG_EXECUTE.DEFAULT);
            if (HR.Failed(hr))
                context.WriteError("Command execution failed with hr = {0:x8}", hr);
        }
    }
}
