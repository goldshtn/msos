using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    static class ClrExceptionExtensions
    {
        // ClrException.Message occasionally throws NullReferenceException even though the exception isn't null
        public static string GetExceptionMessageSafe(this ClrException exception)
        {
            try
            {
                return exception.Message;
            }
            catch (NullReferenceException)
            {
                return "<null>";
            }
        }
    }
}
