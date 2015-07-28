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

        public static string TryDownloadSos(this DataTarget target, ModuleInfo dacInfo)
        {
            Type dataTargetType = target.GetType();
            MethodInfo tryDownloadFile = dataTargetType.GetMethod("TryDownloadFile", BindingFlags.NonPublic | BindingFlags.Instance);
            string sosFileName = Path.GetFileName(dacInfo.FileName)
                .Replace("mscordacwks", "sos")
                .Replace("mscordaccore", "sos");
            return (string)tryDownloadFile.Invoke(target, new object[] {
                sosFileName, (int)dacInfo.TimeStamp, (int)dacInfo.FileSize, target.DefaultSymbolNotification
            });
        }

        public static ClrRuntime CreateRuntimeHack(this DataTarget target, string dacLocation, int major, int minor)
        {
            // FIXME This is a temporary patch for .NET 4.6. The DataTarget.CreateRuntime
            // code incorrectly detects .NET 4.6 and creates a LegacyRuntime instead of the
            // V45Runtime. The result is that the runtime fails to initialize. This was already
            // fixed in a PR https://github.com/Microsoft/dotnetsamples/pull/17 to ClrMD, 
            // but wasn't merged yet.
            string dacFileNoExt = Path.GetFileNameWithoutExtension(dacLocation);
            if (dacFileNoExt.Contains("mscordacwks") && major == 4 && minor >= 5)
            {
                Type dacLibraryType = typeof(DataTarget).Assembly.GetType("Microsoft.Diagnostics.Runtime.DacLibrary");
                object dacLibrary = Activator.CreateInstance(dacLibraryType, target, dacLocation);
                Type v45RuntimeType = typeof(DataTarget).Assembly.GetType("Microsoft.Diagnostics.Runtime.Desktop.V45Runtime");
                object runtime = Activator.CreateInstance(v45RuntimeType, target, dacLibrary);
                return (ClrRuntime)runtime;
            }
            else
            {
                return target.CreateRuntime(dacLocation);
            }
        }
    }
}
