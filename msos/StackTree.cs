using CommandLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.DumpFileNoHeap, TargetType.LiveProcess)]
    [Verb("!StackTree", HelpText = "Display a stack tree of all the target's threads.")]
    class StackTree : ICommand
    {
        [Option("native", HelpText = "Include native threads in the tree.")]
        public bool IncludeNativeThreads { get; set; }

        [Option("depth", Default = 5, HelpText = "The maximum depth of the tree.")]
        public int Depth { get; set; }

        [Option("nothreaddetails", HelpText = "Do not display thread details.")]
        public bool NoThreadDetails { get; set; }

        private CommandExecutionContext _context;

        class ThreadAndStack
        {
            public int ManagedThreadId;
            public uint OSThreadId;
            public IEnumerable<string> Stack;
        }

        public void Execute(CommandExecutionContext context)
        {
            _context = context;

            if (IncludeNativeThreads)
            {
                using (var target = context.CreateTemporaryDbgEngTarget())
                {
                    var tracer = new UnifiedStackTrace(target.DebuggerInterface, context);
                    context.WriteLine("Stack tree for {0} threads:", tracer.NumThreads);
                    var allStacks = from thread in tracer.Threads
                                let frames = from frame in tracer.GetStackTrace(thread.Index)
                                             where frame.Type != UnifiedStackFrameType.Special
                                             select String.Format("{0}!{1}", frame.Module, frame.Method)
                                select new ThreadAndStack
                                {
                                    ManagedThreadId = thread.IsManagedThread ? thread.ManagedThread.ManagedThreadId : 0,
                                    OSThreadId = thread.OSThreadId,
                                    Stack = frames.Reverse()
                                };
                    ProcessStacks(allStacks);
                }
            }
            else
            {
                context.WriteLine("Stack tree for {0} threads:", context.Runtime.Threads.Count);
                var allStacks = from thread in context.Runtime.Threads
                                let frames = from frame in thread.StackTrace
                                             where frame.Kind == ClrStackFrameType.ManagedMethod
                                             select frame.DisplayString
                                select new ThreadAndStack
                                {
                                    ManagedThreadId = thread.ManagedThreadId,
                                    Stack = frames.Reverse()
                                };
                ProcessStacks(allStacks);
            }
        }

        private static IEnumerable<ThreadAndStack> TrimOne(IEnumerable<ThreadAndStack> stacks)
        {
            return from stack in stacks
                   select new ThreadAndStack
                   {
                       ManagedThreadId = stack.ManagedThreadId,
                       OSThreadId = stack.OSThreadId,
                       Stack = stack.Stack.Skip(1)
                   };
        }

        private void ProcessStacks(IEnumerable<ThreadAndStack> stacks, int depth = 0)
        {
            if (depth >= Depth)
                return;

            var grouping = from stack in stacks
                           where stack.Stack.Any()
                           group stack by stack.Stack.First() into g
                           orderby g.Count() descending
                           select g;
            if (grouping.Count() == 1)
            {
                var stackGroup = grouping.First();
                _context.WriteLine("{0}| {1}", new String(' ', depth * 2), stackGroup.Key);
                ProcessStacks(TrimOne(stackGroup), depth);
            }
            else
            {
                foreach (var stackGroup in grouping)
                {
                    _context.Write("{0}+ {1} ",
                        new String(' ', depth * 2), stackGroup.Key);
                    if (!NoThreadDetails)
                    {
                        foreach (var ts in stackGroup)
                        {
                            if (ts.ManagedThreadId != 0)
                            {
                                _context.WriteLink(
                                    String.Format("M{0}", ts.ManagedThreadId),
                                    String.Format("~ {0}; !clrstack", ts.ManagedThreadId)
                                    );
                            }
                            else
                            {
                                _context.WriteLink(
                                    String.Format("OS{0}", ts.OSThreadId),
                                    String.Format("!mk {0}", ts.OSThreadId)
                                    );
                            }
                            _context.Write(" ");
                        }
                    }
                    _context.WriteLine();
                    ProcessStacks(TrimOne(stackGroup), depth + 1);
                }
            }
        }
    }
}
