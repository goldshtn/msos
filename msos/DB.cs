using CommandLine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("db", HelpText="Display memory at the specified address.")]
    class DB : ICommand
    {
        [Value(0, Required = true)]
        public string Address { get; set; }

        [Option('l', HelpText="The number of bytes to display.", Default = 128)]
        public int Length { get; set; }

        [Option('c', HelpText = "The number of columns in each row.", Default = 16)]
        public int Columns { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            ulong address;
            if (!ulong.TryParse(Address, NumberStyles.HexNumber, null, out address))
            {
                context.WriteError("The specified address format is invalid.");
                return;
            }
            if (Length < 1)
            {
                context.WriteError("Length must be at least 1.");
                return;
            }
            if (Columns < 1)
            {
                context.WriteError("Columns must be at least 1.");
                return;
            }

            byte[] buffer = new byte[Columns];
            for (int remaining = Length; remaining > 0; remaining -= Columns, address += (uint)Columns)
            {
                int read = 0;
                if (!context.Runtime.ReadMemory(address, buffer, Math.Min(remaining, Columns), out read))
                {
                    context.WriteError("Error reading memory at {0:x16}, could only read {1} bytes while {2} requested",
                        address, read, Columns);
                    return;
                }
                string bytes = "";
                string chars = "";
                for (int col = 0; col < read; ++col)
                {
                    bytes += String.Format("{0:x2} ", buffer[col]);
                    chars += (buffer[col] >= 32 && buffer[col] <= 126) ? (char)buffer[col] : '.';
                }
                context.WriteLine("{0:x16}  {1}  {2}", address, bytes, chars);
            }
        }
    }
}
