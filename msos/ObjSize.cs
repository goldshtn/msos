using CommandLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!ObjSize", HelpText = "Displays the size of the object graph referenced by the specified object, and including it.")]
    class ObjSize : ICommand
    {
        [Value(0, Required = true)]
        public string ObjectAddress { get; set; }

        [Option("flat", HelpText = "Counts only referenced objects that do not contain further references.")]
        public bool Flat { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            ulong objPtr;
            if (!CommandHelpers.ParseAndVerifyValidObjectAddress(context, ObjectAddress, out objPtr))
                return;

            IEnumerable<Tuple<ulong, ClrType>> subgraph;
            if (Flat)
            {
                subgraph = context.Heap.FlatSubgraphOf(objPtr);
            }
            else
            {
                subgraph = context.Heap.SubgraphOf(objPtr);
            }

            ulong count = 0, size = 0;
            foreach (var objAndType in subgraph)
            {
                ++count;
                size += objAndType.Item2.GetSize(objAndType.Item1);
            }

            context.WriteLine("{0:x16} graph size is {1} objects, {2} bytes ({3})",
                objPtr, count, size, size.ToMemoryUnits());
        }
    }
}
