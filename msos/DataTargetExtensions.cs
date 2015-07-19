using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
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
    }
}
