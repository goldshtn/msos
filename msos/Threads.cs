using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!Threads", HelpText = "Display all managed threads.")]
    class Threads : ICommand
    {
        public void Execute(CommandExecutionContext context)
        {
            context.WriteLine("{0} CLR threads, {1} CLR thread pool threads, {2} background threads",
                context.Runtime.Threads.Count, context.Runtime.Threads.Count(t => t.IsThreadPoolThread()),
                context.Runtime.Threads.Count(t => t.IsBackground));
            context.WriteLine("{0,-6} {1,-6} {2,-6} {3,-6} {4,-20} {5,-20}",
                "MgdId", "OSId", "Lock#", "Apt", "Special", "Exception");
            foreach (var thread in context.Runtime.Threads)
            {
                context.WriteLine("{0,-6} {1,-6} {2,-6} {3,-6} {4,-20} {5,-20}",
                    thread.ManagedThreadId, thread.OSThreadId, thread.LockCount, thread.ApartmentDescription(),
                    thread.SpecialDescription(), thread.CurrentException != null ? thread.CurrentException.Type.Name : "");
            }
        }
    }
}
