using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    static class CommandHelpers
    {
        public static bool VerifyHasHeapIndex(CommandExecutionContext context)
        {
            if (context.HeapIndex == null)
            {
                context.WriteError("This command requires a heap index. Build one with !bhi or load one with !lhi.");
                return false;
            }
            return true;
        }

        public static bool VerifyValidObjectAddress(
            CommandExecutionContext context, ulong objectAddress)
        {
            var heap = context.Runtime.GetHeap();
            var type = heap.GetObjectType(objectAddress);
            
            if (type == null || String.IsNullOrEmpty(type.Name))
            {
                context.WriteError("The specified address does not point to a valid object.");
                return false;
            }

            if (type.IsFree)
            {
                context.WriteError("The specified address points to a free object.");
                return false;
            }

            return true;
        }
    }
}
