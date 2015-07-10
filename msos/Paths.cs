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

            // TODO
        }
    }
}
