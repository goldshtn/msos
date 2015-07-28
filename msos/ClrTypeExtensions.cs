using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    static class ClrTypeExtensions
    {
        public static object GetPrimitiveValueNonBoxed(this ClrType type, ulong address)
        {
            // The ClrType.GetValue implementation assumes that if a primitive
            // is passed in, then it must be boxed, and adds 4/8 bytes to the address
            // specified. We compensate by subtracting 4/8 bytes as necessary.
            return type.GetValue(address - (ulong)IntPtr.Size);
        }
    }
}
