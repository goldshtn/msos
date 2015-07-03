using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    static class ObjectExtensions
    {
        public static string ToStringOrNull(this object @object)
        {
            if (@object == null)
                return "<null>";

            return @object.ToString();
        }
    }
}
