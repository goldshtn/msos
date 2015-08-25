using CmdLine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.LiveProcess)]
    [Verb("!refs", HelpText =
        "Display all objects that have a reference to and from the specified object. " +
        "Requires a heap index, which you can build using !bhi.")]
    class Refs : ICommand
    {
        [Value(0, Required = true, Hexadecimal = true, HelpText = "The object whose references are desired.")]
        public ulong ObjectAddress { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            if (!CommandHelpers.VerifyHasHeapIndex(context))
                return;

            if (!CommandHelpers.VerifyValidObjectAddress(context, ObjectAddress))
                return;

            var type = context.Heap.GetObjectType(ObjectAddress);

            context.WriteLine("Note: unrooted (dead) objects will not have any referencing objects displayed.");
            context.WriteLine("Object {0:x16} ({1}) is referenced by the following objects:", ObjectAddress, type.Name);
            foreach (var referencingObj in context.HeapIndex.FindRefs(ObjectAddress))
            {
                string refHex = String.Format("{0:x16}", referencingObj);
                context.WriteLink(refHex, "!do " + refHex);
                context.WriteLine(" ({0})", context.Heap.GetObjectType(referencingObj).Name);
            }

            context.WriteLine("Object {0:x16} ({1}) references the following objects:", ObjectAddress, type.Name);
            type.EnumerateRefsOfObject(ObjectAddress, (child, _) =>
            {
                var childType = context.Heap.GetObjectType(child);
                if (childType == null || String.IsNullOrEmpty(childType.Name))
                    return;

                string refHex = String.Format("{0:x16}", child);
                context.WriteLink(refHex, "!do " + refHex);
                context.WriteLine(" ({0})", childType.Name);
            });
        }
    }
}
