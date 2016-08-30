using CmdLine;
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
    [SupportedTargets(TargetType.DumpFile, TargetType.DumpFileNoHeap)]
    [Verb(".dec", HelpText =
        "Executes a command through the DbgEng engine. To run extension commands, " +
        "put the .load and the command on the same line. For example: '.dec .loadby " +
        "sos clr; !eeheap; lm'. Alternatively, switch to DbgEng mode and run multiple " +
        "commands by using '.dem'.")]
    class DbgEngCommand : ICommand
    {
        [RestOfInput(Required = true)]
        public string Command { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            if (context.IsInDbgEngNativeMode)
            {
                context.NativeDbgEngTarget.ExecuteDbgEngCommand(Command, context);
            }
            else
            {
                using (var target = context.CreateTemporaryDbgEngTarget())
                {
                    target.ExecuteDbgEngCommand(Command, context);
                }
            }
        }
    }

    [SupportedTargets(TargetType.DumpFile, TargetType.DumpFileNoHeap)]
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
            context.WriteLine("Loading DAC from " + context.DacLocation);
            context.NativeDbgEngTarget.ExecuteDbgEngCommand(
                ".cordll -ve -sd -lp " + Path.GetDirectoryName(context.DacLocation),
                context);

            // SOS hasn't necessarily been loaded at this point; try to load it
            // from the symbol server and then issue the appropriate .load command
            // so that the user can have it immediately available.
            string sosLocation = context.Runtime.TryDownloadSos();
            if (sosLocation == null)
            {
                context.WriteWarning(
                    "Unable to load SOS automatically from symbol server, " +
                    "try to find and .load it manually if needed.");
            }
            else
            {
                context.WriteLine("Loading SOS from " + sosLocation);
                context.NativeDbgEngTarget.ExecuteDbgEngCommand(
                    ".load " + sosLocation,
                    context);
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
            // TODO There are redundant newlines if `text` doesn't end with a newline.
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
