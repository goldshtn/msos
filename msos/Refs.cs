using CommandLine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!refs", HelpText = "Displays all objects that have a reference to and from the specified object.")]
    class Refs : ICommand
    {
        [Value(0, Required = true)]
        public string ObjectAddress { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            if (context.HeapIndex == null)
            {
                context.WriteError("This command requires a heap index. Build one with !bhi or load one with !lhi.");
                return;
            }

            ulong objPtr;
            if (!ulong.TryParse(ObjectAddress, NumberStyles.HexNumber, null, out objPtr))
            {
                context.WriteError("The specified object address format is invalid.");
                return;
            }
            
            var heap = context.Runtime.GetHeap();
            var type = heap.GetObjectType(objPtr);
            if (type == null || type.IsFree || String.IsNullOrEmpty(type.Name))
            {
                context.WriteError("The specified address does not point to a valid object.");
                return;
            }

            context.WriteLine("Object {0:x16} ({1}) is referenced by the following objects:", objPtr, type.Name);
            foreach (var referencingObj in context.HeapIndex.FindRefs(objPtr))
            {
                context.WriteLine("  {0:x16} ({1})", referencingObj, heap.GetObjectType(referencingObj).Name);
            }
            
            context.WriteLine("Object {0:x16} ({1}) references the following objects:", objPtr, type.Name);
            type.EnumerateRefsOfObject(objPtr, (child, _) =>
            {
                var childType = heap.GetObjectType(child);
                if (childType == null || String.IsNullOrEmpty(childType.Name))
                    return;

                context.WriteLine("  {0:x16} ({1})", child, childType.Name);
            });
        }
    }
}
