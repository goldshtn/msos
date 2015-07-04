using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    static class ClrRuntimeExtensions
    {
        public static ClrThread ThreadWithActiveExceptionOrFirstThread(this ClrRuntime runtime)
        {
            return runtime.Threads.FirstOrDefault(t => t.CurrentException != null) ?? runtime.Threads.First();           
        }
    }
}
