using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    class ConsolePrinter : PrinterBase
    {
        private uint _rowsPrinted = 0;

        class ConsoleColorChanger : IDisposable
        {
            private ConsoleColor _oldColor;

            public ConsoleColorChanger(ConsoleColor foregroundColor)
            {
                _oldColor = Console.ForegroundColor;
                Console.ForegroundColor = foregroundColor;
            }

            public void Dispose()
            {
                Console.ForegroundColor = _oldColor;
            }
        }

        public override void WriteInfo(string value)
        {
            using (new ConsoleColorChanger(ConsoleColor.Green))
            {
                Console.Write(value);
            }
        }

        public override void WriteCommandOutput(string value)
        {
            using (new ConsoleColorChanger(ConsoleColor.Gray))
            {
                Console.Write(value);
                if (value.EndsWith(Environment.NewLine))
                {
                    // If paging is enabled, stop after a certain number of lines
                    // and wait for user confirmation before proceeding.
                    ++_rowsPrinted;
                    if (RowsPerPage != 0 && _rowsPrinted >= RowsPerPage)
                    {
                        Console.WriteLine("--- Press any key for more ---");
                        Console.ReadKey(intercept: true);
                        _rowsPrinted = 0;
                    }
                }
            }
        }

        public override void WriteError(string value)
        {
            using (new ConsoleColorChanger(ConsoleColor.Red))
            {
                Console.Write(value);
            }
        }

        public override void WriteWarning(string value)
        {
            using (new ConsoleColorChanger(ConsoleColor.DarkYellow))
            {
                Console.Write(value);
            }
        }

        public override void WriteLink(string text, string command)
        {
            using (new ConsoleColorChanger(ConsoleColor.Blue))
            {
                Console.Write(text);
            }
        }

        public override void ClearScreen()
        {
            Console.Clear();
        }

        public override void CommandEnded()
        {
            _rowsPrinted = 0;
        }
    }
}
