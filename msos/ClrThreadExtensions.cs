using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    static class ClrThreadExtensions
    {
        public static bool IsThreadPoolThread(this ClrThread thread)
        {
            return thread.IsThreadpoolCompletionPort || thread.IsThreadpoolGate || thread.IsThreadpoolTimer || thread.IsThreadpoolWait || thread.IsThreadpoolWorker;
        }

        public static string ApartmentDescription(this ClrThread thread)
        {
            if (thread.IsMTA)
                return "MTA";
            if (thread.IsSTA)
                return "STA";
            return "None";
        }

        public static string SpecialDescription(this ClrThread thread)
        {
            if (thread.IsDebuggerHelper)
                return "DbgHelper";
            if (thread.IsFinalizer)
                return "Finalizer";
            if (thread.IsGC)
                return "GC";
            if (thread.IsShutdownHelper)
                return "ShutdownHelper";
            if (thread.IsAborted)
                return "Aborted";
            if (thread.IsAbortRequested)
                return "AbortRequested";
            if (thread.IsUnstarted)
                return "Unstarted";
            if (thread.IsUserSuspended)
                return "Suspended";
            return "";
        }

        public static void WriteCurrentStackTraceToContext(this ClrThread thread, CommandExecutionContext context)
        {
            WriteStackTraceToContext(thread.StackTrace, context);
        }

        public static void WriteCurrentExceptionStackTraceToContext(this ClrThread thread, CommandExecutionContext context)
        {
            WriteStackTraceToContext(thread.CurrentException.StackTrace, context);
        }

        public static void WriteStackTraceToContext(IList<ClrStackFrame> stackTrace, CommandExecutionContext context)
        {
            // TODO Display the unmanaged stack as well?

            context.WriteLine("{0,-20} {1,-20} {2}", "SP", "IP", "Function");
            foreach (var frame in stackTrace)
            {
                var sourceLocation = frame.GetFileAndLineNumberSafe();
                context.WriteLine("{0,-20:X} {1,-20:X} {2} {3}",
                    frame.StackPointer, frame.InstructionPointer,
                    frame.Method == null ? "<special>" : frame.Method.GetFullSignature(),
                    sourceLocation == null ? "" : String.Format("[{0}:{1}]", sourceLocation.FilePath, sourceLocation.LineNumber));
            }
        }
    }
}
