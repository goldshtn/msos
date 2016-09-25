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
    [Verb("db", HelpText="Display memory at the specified address.")]
    class DB : ICommand
    {
        [Value(0, Required = true, Hexadecimal = true, HelpText = "The memory address to display.")]
        public ulong Address { get; set; }

        [Option('l', HelpText="The number of bytes to display.", Default = 128, Min = 1)]
        public int Length { get; set; }

        [Option('c', HelpText = "The number of columns in each row.", Default = 16, Min = 1)]
        public int Columns { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            byte[] buffer = new byte[Columns];
            for (int remaining = Length; remaining > 0; remaining -= Columns, Address += (uint)Columns)
            {
                int read = 0;
                if (!context.Runtime.ReadMemory(Address, buffer, Math.Min(remaining, Columns), out read))
                {
                    context.WriteErrorLine("Error reading memory at {0:x16}, could only read {1} bytes while {2} requested",
                        Address, read, Columns);
                    return;
                }
                string bytes = "";
                string chars = "";
                for (int col = 0; col < read; ++col)
                {
                    bytes += String.Format("{0:x2} ", buffer[col]);
                    chars += (buffer[col] >= 32 && buffer[col] <= 126) ? (char)buffer[col] : '.';
                }
                context.WriteLine("{0:x16}  {1}  {2}", Address, bytes, chars);
            }
        }
    }
}
