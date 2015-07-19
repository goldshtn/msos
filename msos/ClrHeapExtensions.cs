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
        public static IEnumerable<Tuple<ulong, ClrType>> SubgraphOf(this ClrHeap heap, ulong objPtr)
        {
            return EnumerateFromObjectSet(heap, new[] { objPtr });
        }

        public static ulong SizeReachableFromObjectSet(this ClrHeap heap, IEnumerable<ulong> objects)
        {
            ulong totalSize = 0;
            foreach (var pair in EnumerateFromObjectSet(heap, objects))
            {
                totalSize += pair.Item2.GetSize(pair.Item1);
            }
            return totalSize;
        }

        private static IEnumerable<Tuple<ulong, ClrType>> EnumerateFromObjectSet(ClrHeap heap, IEnumerable<ulong> objects)
        {
            var toGoThrough = new Stack<ulong>(objects);
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

                yield return new Tuple<ulong, ClrType>(obj, type);

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
