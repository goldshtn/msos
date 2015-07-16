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

        [Option("maxPaths", Default = 5, HelpText = "The maximum number of different paths to display.")]
        public int MaxResults { get; set; }

        [Option("maxLocalRoots", Default = 2, HelpText =
            "The maximum number of different paths from local variable roots to display. " + 
            "Specify 0 to avoid seeing any local roots at all.")]
        public int MaxLocalRoots { get; set; }

        [Option("maxDepth", Default = 20, HelpText =
            "The maximum depth of an allowed root path. Set to smaller values if you are getting " + 
            "lots of irrelevant results, or overly long reference chains.")]
        public int MaxDepth { get; set; }

        [Option("parallel", HelpText =
            "Parallelize the index search. This feature is experimental and will not necessarily " +
            "have better performance.")]
        public bool RunInParallel { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            if (!CommandHelpers.VerifyHasHeapIndex(context))
                return;

            ulong objPtr;
            if (!CommandHelpers.ParseAndVerifyValidObjectAddress(context, ObjectAddress, out objPtr))
                return;

            int pathsDisplayed = 0;
            foreach (var path in context.HeapIndex.FindPaths(objPtr, MaxResults, MaxLocalRoots, MaxDepth, RunInParallel))
            {
                context.WriteLine("{0:x16} -> {1:x16} {2}", path.Root.Address, path.Root.Object, path.Root.DisplayText);
                foreach (var obj in path.Chain)
                {
                    string objHex = String.Format("{0:x16}", obj);
                    context.Write("        -> ");
                    context.WriteLink(objHex, "!do " + objHex);
                    context.WriteLine(" {0}", context.Heap.GetObjectType(obj).Name);
                }
                context.WriteLine();
                ++pathsDisplayed;
            }
            context.WriteLine("Total paths displayed: {0}", pathsDisplayed);
            if (pathsDisplayed == 0)
            {
                context.WriteLine("Number of paths may be affected by maximum depth setting. " + 
                    "If you are not seeing enough results, consider increasing --maxDepth.");
            }
        }
    }
}
