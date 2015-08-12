using CmdLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.LiveProcess)]
    [Verb("!ObjSize", HelpText = "Displays the size of the object graph referenced by the specified object, and including it.")]
    class ObjSize : ICommand
    {
        [Value(0, Required = true, Hexadecimal = true, HelpText = "The object whose size is to be displayed.")]
        public ulong ObjectAddress { get; set; }

        [Option("flat", HelpText = "Counts only referenced objects that do not contain further references.")]
        public bool Flat { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            if (!CommandHelpers.VerifyValidObjectAddress(context, ObjectAddress))
                return;

            IEnumerable<Tuple<ulong, ClrType>> subgraph;
            if (Flat)
            {
                subgraph = context.Heap.FlatSubgraphOf(ObjectAddress);
            }
            else
            {
                subgraph = context.Heap.SubgraphOf(ObjectAddress);
            }

            ulong count = 0, size = 0;
            foreach (var objAndType in subgraph)
            {
                ++count;
                size += objAndType.Item2.GetSize(objAndType.Item1);
            }

            context.WriteLine("{0:x16} graph size is {1} objects, {2} bytes ({3})",
                ObjectAddress, count, size, size.ToMemoryUnits());
        }
    }
}
