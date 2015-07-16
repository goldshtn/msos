using CommandLine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!ObjSize", HelpText = "Display the size of the object graph referenced by the specified object, and including it.")]
    class ObjSize : ICommand
    {
        [Value(0, Required = true)]
        public string ObjectAddress { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            ulong objPtr;
            if (!CommandHelpers.ParseAndVerifyValidObjectAddress(context, ObjectAddress, out objPtr))
                return;

            ulong count = 0, size = 0;
            foreach (var objAndType in context.Heap.SubgraphOf(objPtr))
            {
                ++count;
                size += objAndType.Value.GetSize(objAndType.Key);
            }

            context.WriteLine("{0:x16} graph size is {1} objects, {2} bytes", objPtr, count, size);
        }
    }
}
