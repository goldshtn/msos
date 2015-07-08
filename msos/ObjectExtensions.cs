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

        public static bool IsAnonymousType(this object obj)
        {
            if (obj == null)
                return false;

            string typeName = obj.GetType().Name;
            return typeName.StartsWith("<>") && typeName.Contains("AnonymousType");
        }
    }
}
