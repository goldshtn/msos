using CommandLine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!refs", HelpText =
        "Display all objects that have a reference to and from the specified object. " +
        "Requires a heap index, which you can build using !bhi.")]
    class Refs : ICommand
    {
        [Value(0, Required = true)]
        public string ObjectAddress { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            if (!CommandHelpers.VerifyHasHeapIndex(context))
                return;

            ulong objPtr;
            if (!CommandHelpers.ParseAndVerifyValidObjectAddress(context, ObjectAddress, out objPtr))
                return;

            var type = context.Heap.GetObjectType(objPtr);

            context.WriteLine("Note: unrooted (dead) objects will not have any referencing objects displayed.");
            context.WriteLine("Object {0:x16} ({1}) is referenced by the following objects:", objPtr, type.Name);
            foreach (var referencingObj in context.HeapIndex.FindRefs(objPtr))
            {
                context.WriteLine("  {0:x16} ({1})", referencingObj, context.Heap.GetObjectType(referencingObj).Name);
            }

            context.WriteLine("Object {0:x16} ({1}) references the following objects:", objPtr, type.Name);
            type.EnumerateRefsOfObject(objPtr, (child, _) =>
            {
                var childType = context.Heap.GetObjectType(child);
                if (childType == null || String.IsNullOrEmpty(childType.Name))
                    return;

                context.WriteLine("  {0:x16} ({1})", child, childType.Name);
            });
        }
    }
}
