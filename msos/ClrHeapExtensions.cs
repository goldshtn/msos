using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    class HeapTypeStatistics
    {
        public string Type { get; set; }
        public ulong Count { get; set; }
        public ulong Size { get; set; }
        public double AverageSize { get; set; }
        public ulong MinimumSize { get; set; }
        public ulong MaximumSize { get; set; }
    }

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
                    (innerType.ComponentType == null || innerType.ComponentType.ContainsPointers))
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
                                value, innerType.ComponentType));
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

        private static HeapTypeStatistics GetStats(string type, IEnumerable<long> objectSizes)
        {
            ulong min = ulong.MaxValue, max = ulong.MinValue, sum = 0, count = 0;
            foreach (ulong size in objectSizes)
            {
                ++count;
                sum += size;
                min = Math.Min(min, size);
                max = Math.Max(max, size);
            }
            return new HeapTypeStatistics
            {
                Type = type,
                Count = count,
                Size = sum,
                MaximumSize = max,
                MinimumSize = min,
                AverageSize = sum / (double)count
            };
        }

        public static IEnumerable<HeapTypeStatistics> GroupTypesInObjectSetAndSortBySize(this ClrHeap heap, IEnumerable<ulong> addresses)
        {
            // TODO If this LINQ query is too slow, optimize by hand-rolling a loop.
            return from obj in addresses
                   let type = heap.GetObjectType(obj)
                   where type != null && !type.IsFree
                   let size = type.GetSize(obj)
                   group (long)size by type.Name into g
                   let totalSize = g.Sum()
                   orderby totalSize descending
                   select GetStats(g.Key, g);
        }

        public static IDictionary<ClrSegment, ulong> GetFreeSpaceBySegment(this ClrHeap heap)
        {
            var freeSpaceBySegment = new Dictionary<ClrSegment, ulong>();
            foreach (ClrSegment segment in heap.Segments)
            {
                for (ulong currentObject = segment.FirstObject; currentObject != 0;
                    currentObject = segment.NextObject(currentObject))
                {
                    ClrType type = heap.GetObjectType(currentObject);
                    if (type != null && type.IsFree)
                    {
                        ulong size = type.GetSize(currentObject);
                        if (!freeSpaceBySegment.ContainsKey(segment))
                            freeSpaceBySegment.Add(segment, size);
                        else
                            freeSpaceBySegment[segment] += size;
                    }
                }
            }
            return freeSpaceBySegment;
        }
    }
}
