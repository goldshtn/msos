using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    static class ClrStackFrameExtensions
    {
        // ClrStackFrame.GetFileAndLineNumber throws occasionally when something goes wrong with the module
        public static SourceLocation GetFileAndLineNumberSafe(this ClrStackFrame frame)
        {
            try
            {
                return frame.GetFileAndLineNumber();
            }
            catch (ClrDiagnosticsException)
            {
                return null;
            }
        }
    }
}
