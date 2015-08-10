using CmdLine;
using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.DumpFileNoHeap, TargetType.LiveProcess)]
    [Verb("!Threads", HelpText = "Display all threads.")]
    class Threads : ICommand
    {
        [Option("native", HelpText = "Display native threads as well.")]
        public bool DisplayNativeThreads { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            context.WriteLine("{0} CLR threads, {1} CLR thread pool threads, {2} background threads",
                context.Runtime.Threads.Count, context.Runtime.Threads.Count(t => t.IsThreadPoolThread()),
                context.Runtime.Threads.Count(t => t.IsBackground));
            context.WriteLine("{0,-6} {1,-6} {2,-6} {3,-6} {4,-20} {5,-30}",
                "MgdId", "OSId", "Lock#", "Apt", "Special", "Exception");
            foreach (var thread in context.Runtime.Threads)
            {
                context.Write("{0,-6} {1,-6} {2,-6} {3,-6} {4,-20} {5,-30} ",
                    thread.ManagedThreadId, thread.OSThreadId, thread.LockCount, thread.ApartmentDescription(),
                    thread.SpecialDescription(), (thread.CurrentException != null ? thread.CurrentException.Type.Name : "").TrimStartToLength(30));
                context.WriteLink("", String.Format("~ {0}; {1}",
                    thread.ManagedThreadId, thread.CurrentException != null ? "!pe" : "!clrstack"));
                context.WriteLine();
            }

            if (!DisplayNativeThreads)
                return;

            context.WriteLine();
            context.WriteLine("{0,-6} {1,-6} {2,-10} {3}", "OSId", "MgdId", "ExitCode", "StartAddress");
            using (var target = context.CreateTemporaryDbgEngTarget())
            {
                var osThreadIds = target.GetOSThreadIds();
                var symbols = (IDebugSymbols)target.DebuggerInterface;
                var advanced = (IDebugAdvanced2)target.DebuggerInterface;
                int size;
                byte[] buffer = new byte[Marshal.SizeOf(typeof(DEBUG_THREAD_BASIC_INFORMATION))];
                GCHandle gch = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    for (uint engineThreadId = 0; engineThreadId < osThreadIds.Length; ++engineThreadId)
                    {
                        if (0 != advanced.GetSystemObjectInformation(DEBUG_SYSOBJINFO.THREAD_BASIC_INFORMATION, 0,
                            engineThreadId, buffer, buffer.Length, out size))
                            continue;

                        var info = (DEBUG_THREAD_BASIC_INFORMATION)Marshal.PtrToStructure(
                            gch.AddrOfPinnedObject(), typeof(DEBUG_THREAD_BASIC_INFORMATION));
                        var managedThread = context.Runtime.Threads.SingleOrDefault(t => t.OSThreadId == osThreadIds[engineThreadId]);
                        context.Write("{0,-6} {1,-6} {2,-10} {3:x16} ", osThreadIds[engineThreadId],
                            managedThread != null ? managedThread.ManagedThreadId.ToString() : "",
                            info.ExitStatus == 259 ? "active" : info.ExitStatus.ToString(), info.StartOffset);
                        
                        uint symSize;
                        ulong displacement;
                        StringBuilder symbolName = new StringBuilder(2048);
                        if (0 == symbols.GetNameByOffset(info.StartOffset, symbolName, symbolName.Capacity, out symSize, out displacement))
                        {
                            context.Write("{0} ", symbolName.ToString());
                        }

                        context.WriteLink("", String.Format("!mk {0}", osThreadIds[engineThreadId]));
                        context.WriteLine();
                    }
                }
                finally
                {
                    gch.Free();
                }
            }
        }
    }
}
