using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    static class DataTargetExtensions
    {
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
