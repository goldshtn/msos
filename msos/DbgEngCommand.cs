using CommandLine;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb(".dec", HelpText =
        "Executes a command through the DbgEng engine. To run extension commands, " +
        "put the .load and the command on the same line. For example: '.dec .loadby " +
        "sos clr; !eeheap; lm'. Alternatively, switch to DbgEng mode and run multiple " +
        "commands by using '.dem'.")]
    class DbgEngCommand : ICommand
    {
        [Value(0, Required = true)]
        public IEnumerable<string> Command { get; set; }

        private string RealCommand { get { return String.Join(" ", Command.ToArray()); } }

        public void Execute(CommandExecutionContext context)
        {
            if (context.IsInDbgEngNativeMode)
            {
                context.NativeDbgEngTarget.ExecuteDbgEngCommand(RealCommand, context);
            }
            else
            {
                using (var target = context.CreateTemporaryDbgEngTarget())
                {
                    target.ExecuteDbgEngCommand(RealCommand, context);
                }
            }
        }
    }
    
    [Verb(".dem", HelpText =
        "Switches to DbgEng mode to execute multiple native DbgEng commands. " +
        "To switch back, run 'q'.")]
    class SwitchToDbgEngMode : ICommand
    {
        public void Execute(CommandExecutionContext context)
        {
            context.EnterDbgEngNativeMode();

            // In case the user is going to use sos/sosex, make sure they have
            // the appropriate DAC location configured.
            context.NativeDbgEngTarget.ExecuteDbgEngCommand(
                ".cordll -ve -sd -lp " + Path.GetDirectoryName(context.DacLocation),
                context);
            
            // TODO But SOS hasn't been loaded at this point; can try to load it
            // from the symbol server and then issue the appropriate .load command.
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
                    _context.WriteError(text.TrimEnd('\n', '\r'));
                    break;
                case DEBUG_OUTPUT.EXTENSION_WARNING:
                case DEBUG_OUTPUT.WARNING:
                    _context.WriteWarning(text.TrimEnd('\n', '\r'));
                    break;
                case DEBUG_OUTPUT.SYMBOLS:
                    _context.WriteInfo(text.TrimEnd('\n', '\r'));
                    break;
                default:
                    _context.Write(text);
                    break;
            }

            return 0;
        }
    }
}
