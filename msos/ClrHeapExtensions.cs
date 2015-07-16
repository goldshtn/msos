using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    static class ClrHeapExtensions
    {
        public static IEnumerable<KeyValuePair<ulong, ClrType>> SubgraphOf(this ClrHeap heap, ulong objPtr)
        {
            var toGoThrough = new Stack<ulong>();
            toGoThrough.Push(objPtr);
            var seen = new ObjectSet(heap);

            while (toGoThrough.Count > 0)
            {
                var obj = toGoThrough.Pop();
                if (seen.Contains(obj))
                    continue;

                seen.Add(obj);
                
                var type = heap.GetObjectType(obj);
                if (type == null || type.IsFree || String.IsNullOrEmpty(type.Name))
                    continue;

                yield return new KeyValuePair<ulong, ClrType>(obj, type);

                type.EnumerateRefsOfObject(obj, (child, _) =>
                {
                    if (child != 0 && !seen.Contains(child))
                    {
                        toGoThrough.Push(child);
                    }
                });
            }
        }
    }
}
