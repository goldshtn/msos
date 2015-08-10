using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmdLine
{
    static class Extensions
    {
        private static readonly HashSet<Type> _numericTypes = new HashSet<Type>()
        {
            typeof(byte),
            typeof(sbyte),
            typeof(char),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double)
        };

        public static bool IsNumeric(this Type type)
        {
            return _numericTypes.Contains(type);
        }

        public static string SplitToLines(this string original, int columns = 80, int prepadSpaces = 0)
        {
            if (String.IsNullOrEmpty(original))
                return "";

            string[] parts = original.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder result = new StringBuilder();
            int currentCol = 0;
            bool firstLine = true;
            for (int i = 0; i < parts.Length; ++i)
            {
                string part = parts[i];
                if ((currentCol != 0) && (currentCol + part.Length + 1 > columns))
                {
                    result.AppendLine();
                    firstLine = false;
                    currentCol = 0;
                }
                if (currentCol == 0 && !firstLine)
                {
                    result.Append(new string(' ', prepadSpaces));
                }
                result.Append(part);
                if (i < parts.Length - 1)
                {
                    result.Append(" ");
                }
                currentCol += part.Length + 1;
            }
            return result.ToString();
        }
    }
}
