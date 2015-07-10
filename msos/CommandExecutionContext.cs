using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    class CommandExecutionContext : IDisposable
    {
        public bool ShouldQuit { get; set; }
        public ClrRuntime Runtime { get; set; }
        public int CurrentManagedThreadId { get; set; }
        public string DumpFile { get; set; }
        public int ProcessId { get; set; }
        public string DacLocation { get; set; }
        public HeapIndex HeapIndex { get; set; }
        public ClrHeap Heap { get; set; }
        public IPrinter Printer { get; set; }

        public ClrThread CurrentThread
        {
            get
            {
                return Runtime.Threads.FirstOrDefault(t => t.ManagedThreadId == CurrentManagedThreadId);
            }
        }

        public void WriteLine(string format, params object[] args)
        {
            Printer.WriteCommandOutput(format, args);
        }

        public void WriteLine(string value)
        {
            Printer.WriteCommandOutput(value);
        }

        public void WriteError(string format, params object[] args)
        {
            Printer.WriteError(format, args);
        }

        public void WriteError(string value)
        {
            Printer.WriteError(value);
        }

        public void WriteWarning(string format, params object[] args)
        {
            Printer.WriteWarning(format, args);
        }

        public void WriteWarning(string value)
        {
            Printer.WriteWarning(value);
        }

        public void WriteInfo(string format, params object[] args)
        {
            Printer.WriteInfo(format, args);
        }

        public void WriteInfo(string value)
        {
            Printer.WriteInfo(value);
        }

        public void Dispose()
        {
            Printer.Dispose();
        }
    }
}
