using CommandLine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb(".formats", HelpText = "Formats a hexadecimal 64-bit value in multiple representations.")]
    class Formats : ICommand
    {
        [Value(0, Required = true)]
        public string Value { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            ulong value;
            if (!ulong.TryParse(Value, NumberStyles.HexNumber, null, out value))
            {
                context.WriteError("The provided value is not a valid hexadecimal 64-bit number.");
                return;
            }

            context.WriteLine("Hex:      {0:x16}", value);
            context.WriteLine("Decimal:  {0}", value);
            context.WriteLine("Binary:   {0}", Convert.ToString((long)value, 2).PadLeft(16, '0'));
            context.WriteLine("Float:    {0}", BitConverter.ToSingle(BitConverter.GetBytes(value), 0));
            context.WriteLine("Double:   {0}", BitConverter.Int64BitsToDouble((long)value));
        }
    }
}
