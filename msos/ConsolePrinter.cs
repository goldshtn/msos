using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    class ConsolePrinter
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

        public static void WriteInfo(string format, params object[] args)
        {
            using (new ConsoleColorChanger(ConsoleColor.Green))
            {
                Console.WriteLine(format, args);
            }
        }

        public static void WriteCommandOutput(string format, params object[] args)
        {
            WriteCommandOutput(String.Format(format, args));
        }

        public static void WriteCommandOutput(string value)
        {
            using (new ConsoleColorChanger(ConsoleColor.Gray))
            {
                Console.WriteLine(value);
            }
        }

        public static void WriteError(string format, params object[] args)
        {
            WriteError(String.Format(format, args));
        }

        public static void WriteError(string value)
        {
            using (new ConsoleColorChanger(ConsoleColor.Red))
            {
                Console.WriteLine(value);
            }
        }

        public static void WriteWarning(string format, object[] args)
        {
            using (new ConsoleColorChanger(ConsoleColor.DarkYellow))
            {
                Console.WriteLine(format, args);
            }
        }
    }
}
