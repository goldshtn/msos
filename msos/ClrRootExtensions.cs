using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    public static class ClrRootExtensions
    {
        public static string BetterToString(this ClrRoot root)
        {
            if (root.Kind == GCRootKind.LocalVar && root.Thread != null)
            {
                return String.Format("{0} thread {1}", root.Name, root.Thread.ManagedThreadId);
            }
            return root.Name;
        }
    }
}
