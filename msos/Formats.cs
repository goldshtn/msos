using CmdLine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.DumpFileNoHeap, TargetType.LiveProcess)]
    [Verb(".formats", HelpText = "Formats a hexadecimal 64-bit value in multiple representations.")]
    class Formats : ICommand
    {
        [Value(0, Required = true, Hexadecimal = true, HelpText = "The value to format.")]
        public ulong Value { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            context.WriteLine("Hex:      {0:x16}", Value);
            context.WriteLine("Decimal:  {0}", Value);
            context.WriteLine("Binary:   {0}", Convert.ToString((long)Value, 2).PadLeft(16, '0'));
            context.WriteLine("Float:    {0}", BitConverter.ToSingle(BitConverter.GetBytes(Value), 0));
            context.WriteLine("Double:   {0}", BitConverter.Int64BitsToDouble((long)Value));
        }
    }
}
