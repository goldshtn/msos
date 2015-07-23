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
        /// <summary>
        /// Returns only referenced objects that do not contain further pointers,
        /// and referenced arrays of objects that do not contain further pointers.
        /// This provides a fast estimate of the amount of memory that is likely 
        /// only retained by the target object.
        /// </summary>
        public static IEnumerable<Tuple<ulong, ClrType>> FlatSubgraphOf(this ClrHeap heap, ulong objPtr)
        {
            var type = heap.GetObjectType(objPtr);
            if (type == null || type.IsFree || String.IsNullOrEmpty(type.Name))
                yield break;

            yield return new Tuple<ulong, ClrType>(objPtr, type);

            List<Tuple<ulong, ClrType>> results = new List<Tuple<ulong, ClrType>>();
            type.EnumerateRefsOfObject(objPtr, (inner, offset) =>
            {
                var innerType = heap.GetObjectType(inner);

                if (innerType == null || innerType.IsFree)
                {
                    return;
                }
                if (innerType.IsArray &&
                    (innerType.ArrayComponentType == null || innerType.ArrayComponentType.ContainsPointers))
                {
                    return;
                }
                if (!innerType.IsArray && innerType.ContainsPointers)
                {
                    return;
                }

                results.Add(new Tuple<ulong, ClrType>(inner, innerType));
                
                // Value type instances are sized along with the containing array.
                if (innerType.IsArray && innerType.ElementType != ClrElementType.Struct)
                {
                    var arraySize = innerType.GetArrayLength(inner);
                    for (int i = 0; i < arraySize; ++i)
                    {
                        var address = innerType.GetArrayElementAddress(inner, i);
                        ulong value;
                        if (heap.ReadPointer(address, out value))
                        {
                            results.Add(new Tuple<ulong, ClrType>(
                                value, innerType.ArrayComponentType));
                        }
                    }
                }
            });

            foreach (var result in results)
                yield return result;
        }

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
