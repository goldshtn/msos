using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    static class StringExtensions
    {
        public static string TrimStartToLength(this string str, int length)
        {
            if (str.Length > length - 3)
            {
                return "..." + str.Substring(str.Length - (length - 3));
            }
            return str;
        }

        public static string TrimEndToLength(this string str, int length)
        {
            if (str.Length > length - 3)
            {
                return str.Substring(0, length - 3) + "...";
            }
            return str;
        }
    }
}
