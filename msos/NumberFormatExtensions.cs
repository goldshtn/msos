using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    static class NumberFormatExtensions
    {
        public static string ToMemoryUnits(this ulong size)
        {
            if (size < 1024ul)
                return size.ToString() + "b";

            if (size < 1048576ul)
                return String.Format("{0:#,0.000}kb", size / 1024.0);

            if (size < 1073741824ul)
                return String.Format("{0:#,0.000}mb", size / 1048576.0);

            return String.Format("{0:#,0.000}gb", size / 1073741824.0);
        }
    }
}
