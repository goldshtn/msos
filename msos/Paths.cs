using CommandLine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!paths", HelpText =
        "Display paths from GC roots leading to the specified object. Requires a heap index, " +
        "which you can build using !bhi. To work without a heap index, use !GCRoot, which can be much slower.")]
    class Paths : ICommand
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

            foreach (var path in context.HeapIndex.FindPaths(objPtr))
            {
                context.WriteLine("{0:x16} -> {1:x16} {2}", path.Root.Address, path.Root.Object, path.Root.Name);
                foreach (var obj in path.Chain)
                {
                    context.WriteLine("        -> {0:x16} {1}", obj, context.Heap.GetObjectType(obj).Name);
                }
            }

            // TODO Store the paths and print only the shortest path leading to a certain root,
            // based on an option provided by the user. Also allow to pick whether we want a specific
            // root, only locals, only statics, etc.
            // Maybe even allow prioritization by specific types (when there is a choice of multiple
            // referencing chunks, prefer specific object types to be followed first).
        }
    }
}
