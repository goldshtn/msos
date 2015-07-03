using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!ThreadPool", HelpText = "Displays information about the CLR thread pool.")]
    class ThreadPool : ICommand
    {
        public void Execute(CommandExecutionContext context)
        {
            var threadPool = context.Runtime.GetThreadPool();
            context.WriteLine("Total threads:   {0}", threadPool.TotalThreads);
            context.WriteLine("Running threads: {0}", threadPool.RunningThreads);
            context.WriteLine("Idle threads:    {0}", threadPool.IdleThreads);
            context.WriteLine("Max threads:     {0}", threadPool.MaxThreads);
            context.WriteLine("Min threads:     {0}", threadPool.MinThreads);
            context.WriteLine("CPU utilization: {0}% (estimated)", threadPool.CpuUtilization);
        }
    }
}
