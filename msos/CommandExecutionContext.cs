using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    class CommandExecutionContext
    {
        public CommandExecutionContext(ClrRuntime runtime)
        {
            Runtime = runtime;
            CurrentManagedThreadId = 0;
        }

        public bool ShouldQuit { get; set; }
        public ClrRuntime Runtime { get; set; }
        public int CurrentManagedThreadId { get; set; }

        public ClrThread CurrentThread
        {
            get
            {
                return Runtime.Threads.FirstOrDefault(t => t.ManagedThreadId == CurrentManagedThreadId);
            }
        }

        public void WriteLine(string format, params object[] args)
        {
            ConsolePrinter.WriteCommandOutput(format, args);
        }

        public void WriteError(string format, params object[] args)
        {
            ConsolePrinter.WriteError(format, args);
        }

        public void WriteWarning(string format, params object[] args)
        {
            ConsolePrinter.WriteWarning(format, args);
        }
    }
}
