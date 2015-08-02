using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.LiveProcess)]
    [Verb("!dso", HelpText =
        "Dump stack objects for the specified thread. Not all objects displayed are actually " +
        "referenced by live local variables.")]
    class DumpStackObjects : ICommand
    {
        public void Execute(CommandExecutionContext context)
        {
            context.WriteLine("{0,-20} {1,-20} {2}", "Address", "Object", "Type");
            foreach (var localRoot in context.CurrentThread.EnumerateStackObjects())
            {
                var type = context.Heap.GetObjectType(localRoot.Object);

                context.Write("{0,-20:x16} {1,-20:x16} {2} ", localRoot.Address, localRoot.Object,
                    type == null ? "" : type.Name);
                if (type != null && !type.IsFree)
                {
                    context.WriteLink("", String.Format("!do {0:x16}", localRoot.Object));
                }
                context.WriteLine();
            }
        }
    }
}
