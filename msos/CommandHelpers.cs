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

        public static bool ParseAndVerifyValidObjectAddress(
            CommandExecutionContext context, string objectAddress, out ulong objPtr)
        {
            if (!ulong.TryParse(objectAddress, NumberStyles.HexNumber, null, out objPtr))
            {
                context.WriteError("The specified object address format is invalid.");
                return false;
            }

            var heap = context.Runtime.GetHeap();
            var type = heap.GetObjectType(objPtr);
            
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
