using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    static class HR
    {
        public const int S_OK = 0;
        public const int S_FALSE = 1;

        public static bool Failed(int hr)
        {
            return hr < 0;
        }

        public static bool Succeeded(int hr)
        {
            return hr >= 0;
        }
    }
}
