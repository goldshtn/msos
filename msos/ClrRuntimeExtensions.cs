using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        /// <summary>
        /// ClrMD does not expose the IXCLR* interfaces directly, but a lot of wrapper work is already
        /// done, including providing the ICLRDataTarget implementation these interfaces require. So,
        /// instead of repeating all this work, we grab an internal field off the RuntimeBase class and 
        /// hope for the best.
        /// </summary>
        public static IXCLRDataProcess GetCLRDataProcess(this ClrRuntime runtime)
        {
            // TODO Make sure the m_dacInterface is always initialized by the time we get here
            Type runtimeType = runtime.GetType();
            FieldInfo dacField = runtimeType.GetField("m_dacInterface", BindingFlags.NonPublic | BindingFlags.Instance);
            object dacInterface = dacField.GetValue(runtime);
            return (IXCLRDataProcess)dacInterface;
        }
    }
}
