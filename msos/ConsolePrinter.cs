using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    class ConsolePrinter : PrinterBase
    {
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
                Console.WriteLine(value);
            }
        }

        public override void WriteCommandOutput(string value)
        {
            using (new ConsoleColorChanger(ConsoleColor.Gray))
            {
                Console.WriteLine(value);
            }
        }

        public override void WriteError(string value)
        {
            using (new ConsoleColorChanger(ConsoleColor.Red))
            {
                Console.WriteLine(value);
            }
        }

        public override void WriteWarning(string value)
        {
            using (new ConsoleColorChanger(ConsoleColor.DarkYellow))
            {
                Console.WriteLine(value);
            }
        }

        public override void ClearScreen()
        {
            Console.Clear();
        }
    }
}
