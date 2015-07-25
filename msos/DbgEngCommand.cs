using CommandLine;
using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("dec", HelpText = "")]
    class DbgEngCommand : ICommand
    {
        [Value(0, Required = true)]
        public IEnumerable<string> Command { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            using (var target = context.CreateDbgEngTarget())
            {
                var outputCallbacks = new OutputCallbacks(context);
                msos_IDebugClient5 client = (msos_IDebugClient5)target.DebuggerInterface;
                HR.Verify(client.SetOutputCallbacksWide(outputCallbacks));

                IDebugControl6 control = (IDebugControl6)target.DebuggerInterface;
                HR.Verify(
                    control.ExecuteWide(
                        DEBUG_OUTCTL.THIS_CLIENT,
                        String.Join(" ", Command.ToArray()),
                        DEBUG_EXECUTE.DEFAULT)
                    );
            }
        }
    }

    class OutputCallbacks : IDebugOutputCallbacksWide
    {
        private CommandExecutionContext _context;

        public OutputCallbacks(CommandExecutionContext context)
        {
            _context = context;
        }

        public int Output(DEBUG_OUTPUT mask, string text)
        {
            switch (mask)
            {
                case DEBUG_OUTPUT.ERROR:
                    _context.WriteError(text);
                    break;
                case DEBUG_OUTPUT.EXTENSION_WARNING:
                case DEBUG_OUTPUT.WARNING:
                    _context.WriteWarning(text);
                    break;
                case DEBUG_OUTPUT.SYMBOLS:
                    _context.WriteInfo(text);
                    break;
                default:
                    _context.Write(text);
                    break;
            }

            return 0;
        }
    }
}
