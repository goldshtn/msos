using CommandLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.DumpFileNoHeap, TargetType.LiveProcess)]
    [Verb("!PrintException", HelpText = "Display the current exception or the specified exception object.")]
    class PrintException : ICommand
    {
        [Value(0)]
        public string ExceptionAddress { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            if (!String.IsNullOrEmpty(ExceptionAddress))
            {
                ulong exceptionPtr;
                if (!ulong.TryParse(ExceptionAddress, NumberStyles.HexNumber, null, out exceptionPtr))
                {
                    context.WriteError("The specified exception address has an invalid format.");
                    return;
                }

                var heap = context.Runtime.GetHeap();
                var type = heap.GetObjectType(exceptionPtr);
                if (type == null || !type.IsException)
                {
                    context.WriteError("The specified address is not the address of an Exception-derived object.");
                    return;
                }

                DisplayException(heap.GetExceptionObject(exceptionPtr), context);
            }
            else
            {
                var thread = context.CurrentThread;
                if (thread == null)
                {
                    context.WriteError("There is no current managed thread");
                    return;
                }
                if (thread.CurrentException == null)
                {
                    context.WriteLine("There is no current managed exception on this thread");
                    return;
                }
                DisplayException(thread.CurrentException, context);
            }
        }

        private static void DisplayException(ClrException exception, CommandExecutionContext context)
        {
            context.WriteLine("Exception object: {0:x16}", exception.Address);
            context.WriteLine("Exception type:   {0}", exception.Type.Name);
            if (context.TargetType != TargetType.DumpFileNoHeap)
            {
                // In no-heap dumps, stuff goes wrong inside DesktopException because some fields can't
                // be found. It can be fixed with a lot of refactoring (not relying on fields at all and
                // using offsets instead), but it's probably not such a big deal anyway.
                var innerException = exception.Inner;
                context.WriteLine("Message:          {0}", exception.GetExceptionMessageSafe());
                context.WriteLine("Inner exception:  {0}", innerException == null ? "<none>" : String.Format("{0:x16}", innerException.Address));
                context.WriteLine("HResult:          {0:x}", exception.HResult);
            }
            context.WriteLine("Stack trace:");
            ClrThreadExtensions.WriteStackTraceToContext(null, exception.StackTrace, context, displayArgumentsAndLocals: false);
        }
    }

    [SupportedTargets(TargetType.DumpFile, TargetType.DumpFileNoHeap, TargetType.LiveProcess)]
    [Verb("!pe", HelpText = "Display the current exception or the specified exception object.")]
    class PE : PrintException
    {
    }
}
