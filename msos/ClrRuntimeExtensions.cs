using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
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

        public static string TryDownloadSos(this ClrRuntime runtime)
        {
            string sosFileName;
            string dacLocation = runtime.ClrInfo.LocalMatchingDac;
            if (dacLocation != null)
            {
                // If we have a local DAC, we probably also have a local SOS.
                sosFileName = GetSosFileName(dacLocation);
                if (File.Exists(sosFileName))
                {
                    return sosFileName;
                }
            }

            var symbolLocator = runtime.DataTarget.SymbolLocator;
            var dacInfo = runtime.ClrInfo.DacInfo;
            if (dacLocation == null)
            {
                dacLocation = symbolLocator.FindBinary(dacInfo);
            }
            
            // We couldn't find the DAC on the symbol server, so SOS won't help us.
            if (dacLocation == null)
                return null;

            // We don't have a local DAC, so try downloading SOS from the symbol server.
            sosFileName = GetSosFileName(Path.GetFileName(dacLocation));
            return symbolLocator.FindBinary(sosFileName, dacInfo.TimeStamp, dacInfo.FileSize, checkProperties: false);
        }

        private static string GetSosFileName(string dacFileName)
        {
            return dacFileName
                .Replace("mscordacwks", "sos")
                .Replace("mscordaccore", "sos");
        }
    }
}
