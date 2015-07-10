using CommandLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!bhi", HelpText = "Builds a heap index that can be used for efficient object queries and stores it to the specified file.")]
    class BuildHeapIndex : ICommand
    {
        [Value(0, Required = true)]
        public string HeapIndexFileName { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            context.HeapIndex = new HeapIndex(context);
            context.HeapIndex.Build(HeapIndexFileName);
        }
    }

    [Verb("!chi", HelpText = "Clears the heap index from memory. The on-disk file is not affected.")]
    class ClearHeapIndex : ICommand
    {
        public void Execute(CommandExecutionContext context)
        {
            context.HeapIndex = null;
        }
    }

    [Verb("!lhi", HelpText = "Loads a heap index from the specified file.")]
    class LoadHeapIndex : ICommand
    {
        [Value(0, Required = true)]
        public string HeapIndexFileName { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            context.HeapIndex = new HeapIndex(context);
            context.HeapIndex.Load(HeapIndexFileName);
        }
    }

    class HeapIndex
    {
        const int ChunkSize = 1024;

        // Serialized object sizes and (de)serialization time can be driven down by using:
        //  - ulong[] for _chunkIdToFirstNonFreeObjectInChunk, it's all sequential chunk ids anyway
        //  - int[] instead of HashSet<int> in _chunkToReferencingChunk -- construct with HashSet but store with array
        private Dictionary<int, ulong> _chunkIdToFirstNonFreeObjectInChunk = new Dictionary<int, ulong>();
        private Dictionary<ulong, int> _startOfChunkToChunkId = new Dictionary<ulong, int>();
        private Dictionary<int, HashSet<int>> _chunkToReferencingChunks = new Dictionary<int, HashSet<int>>();
        
        private int _objectsWithMissingChunkIds = 0;
        private int _lastChunkId = 0;
        
        private HashSet<ulong> _directlyRooted = new HashSet<ulong>();
        private List<ClrRoot> _allRoots;

        private CommandExecutionContext _context;
        private ClrHeap _heap;

        public HeapIndex(CommandExecutionContext context)
        {
            _context = context;
            _heap = context.Runtime.GetHeap();
            _allRoots = new List<ClrRoot>(_heap.EnumerateRoots(enumerateStatics: true));
        }

        public void Load(string indexFileName)
        {
            Measure(() =>
            {
                using (var file = File.OpenRead(indexFileName))
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    Indexes indexes = (Indexes)formatter.Deserialize(file);
                    _chunkIdToFirstNonFreeObjectInChunk = indexes.ChunkIdToFirstNonFreeObjectInChunk;
                    _startOfChunkToChunkId = indexes.StartOfChunkToChunkId;
                    _chunkToReferencingChunks = indexes.ChunkToReferencingChunks;
                    _directlyRooted = indexes.DirectlyRooted;
                }
            }, "Loading index from disk");
        }

        public void Build(string indexFileName)
        {
            // Build an index of N-byte chunks in all heap segments. The index is from chunk-id (int?) to
            // the first non-free object in the chunk (not to start of the chunk, which could be in the middle
            // of an object). If a chunk is completely empty or doesn't contain the start of any object, it
            // doesn't have an id.
            Measure(BuildChunks, "Building chunks");

            // Traverse all object relationships on the heap from roots. For each chunk, specify which other
            // chunks contain references to objects in that chunk. When traversing this information, we need to
            // keep in mind that roots can also contain references to objects in the chunk -- we don't store this
            // information again because it's very easy to obtain by enumerating the roots. This decision can be
            // changed if root enumeration turns out to be slow when there are many roots.
            // Note that only live objects are being enumerated. For dead objects, it's not interesting to ask who
            // has a reference to the object -- because the referencing object is also dead.
            Measure(BuildChunkIndex, "Building chunk index");

            DisplayStatistics();

            Measure(() => Save(indexFileName), "Saving index to disk");
        }

        [Serializable]
        class Indexes
        {
            public Dictionary<int, ulong> ChunkIdToFirstNonFreeObjectInChunk;
            public Dictionary<ulong, int> StartOfChunkToChunkId;
            public Dictionary<int, HashSet<int>> ChunkToReferencingChunks;
            public HashSet<ulong> DirectlyRooted;
        }

        private void Save(string indexFileName)
        {
            using (var file = File.OpenWrite(indexFileName))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                var indexes = new Indexes();
                indexes.ChunkIdToFirstNonFreeObjectInChunk = _chunkIdToFirstNonFreeObjectInChunk;
                indexes.StartOfChunkToChunkId = _startOfChunkToChunkId;
                indexes.ChunkToReferencingChunks = _chunkToReferencingChunks;
                indexes.DirectlyRooted = _directlyRooted;
                formatter.Serialize(file, indexes);
            }
            _context.WriteLine("Wrote index file of size {0}",
                ((ulong)new FileInfo(indexFileName).Length).ToMemoryUnits());
        }

        private void Measure(Action what, string description)
        {
            Stopwatch sw = Stopwatch.StartNew();
            what();
            _context.WriteLine("{0} took {1} ms", description, sw.ElapsedMilliseconds);
        }

        public class RootPath
        {
            public ClrRoot Root;
            public ulong[] Chain;
        }

        abstract class RootPathFinder
        {
            public bool OnlyOnePathPerRoot { get; set; }
            public int MaxResults { get; set; }

            protected HeapIndex Index { get; private set; }
            protected ObjectSet Visited { get; private set; }
            protected ulong TargetObject { get; private set; }
            protected bool Stop { get; private set; }
            
            private List<RootPath> _paths = new List<RootPath>();
            private Dictionary<ulong, HashSet<ClrRoot>> _rootsSeenReferencingObject = new Dictionary<ulong, HashSet<ClrRoot>>();

            public IEnumerable<RootPath> Paths { get { return _paths; } }

            protected RootPathFinder(HeapIndex index, ulong targetObject)
            {
                Index = index;
                TargetObject = targetObject;
                Visited = new ObjectSet(Index._heap);
            }

            protected void AddPath(RootPath path)
            {
                _paths.Add(path);
            }

            public abstract void FindPaths();

            protected void AddPathIfRootReached(ulong obj, IEnumerable<ulong> path)
            {
                if (Index._directlyRooted.Contains(obj))
                {
                    ulong[] chainArray = path.ToArray();
                    foreach (var root in Index._allRoots.Where(r => r.Object == obj))
                    {
                        HashSet<ClrRoot> seenRoots;
                        if (!_rootsSeenReferencingObject.TryGetValue(obj, out seenRoots))
                        {
                            seenRoots = new HashSet<ClrRoot>();
                            _rootsSeenReferencingObject.Add(obj, seenRoots);
                        }

                        // Only accept one local root per object. There can be literally thousands of
                        // local variables pointing to each object, and it's unnecessary garbage to display
                        // them all.
                        // TODO Consider further restricting the number of different local roots that
                        //      can lead to an object, because even with these restrictions it's potentially
                        //      way too many roots.
                        if (OnlyOnePathPerRoot &&
                            (seenRoots.Contains(root) || (root.Kind == GCRootKind.LocalVar &&
                                                          seenRoots.Any(r => r.Object == obj)))
                            )
                        {
                            continue;
                        }

                        seenRoots.Add(root);

                        AddPath(new RootPath { Root = root, Chain = chainArray });
                        
                        if (_paths.Count >= MaxResults)
                        {
                            Stop = true;
                            break;
                        }
                    }
                }
            }
        }

        class DepthFirstRootPathFinder : RootPathFinder
        {
            public DepthFirstRootPathFinder(HeapIndex index, ulong targetObject)
                : base(index, targetObject)
            {
            }

            public override void FindPaths()
            {
                var path = new Stack<ulong>();
                path.Push(TargetObject);
                FindPaths(TargetObject, path);
            }

            private void FindPaths(ulong current, Stack<ulong> path)
            {
                if (Stop)
                    return;

                if (Visited.Contains(current))
                    return;

                Visited.Add(current);

                AddPathIfRootReached(current, path);

                foreach (ulong referencingObj in Index.FindRefs(current))
                {
                    path.Push(referencingObj);
                    FindPaths(referencingObj, path);
                    path.Pop();
                }
            }
        }

        class BreadthFirstRootPathFinder : RootPathFinder
        {
            // TODO Once we find a path of length N, consider only further paths of length up to N+m1
            // if they are leading to the same root. Can be even more restrictive and disallow any further
            // paths to be more than length N+m2, regardless of the root they reach (this can save some
            // time on the heap walking).

            public BreadthFirstRootPathFinder(HeapIndex index, ulong targetObject)
                : base(index, targetObject)
            {
            }

            public override void FindPaths()
            {
                var evalQueue = new Queue<ulong[]>();
                evalQueue.Enqueue(new ulong[] { TargetObject });
                while (evalQueue.Count > 0)
                {
                    if (Stop)
                        break;

                    var chain = evalQueue.Dequeue();
                    var current = chain[0];

                    if (Visited.Contains(current))
                        continue;

                    Visited.Add(current);

                    AddPathIfRootReached(current, chain);

                    foreach (var referencingObj in Index.FindRefs(current))
                    {
                        var newChain = new ulong[chain.Length + 1];
                        newChain[0] = referencingObj;
                        Array.Copy(chain, 0, newChain, 1, chain.Length);
                        evalQueue.Enqueue(newChain);
                    }
                }
            }
        }

        public IEnumerable<RootPath> FindPaths(ulong targetObj)
        {
            var pathFinder = new BreadthFirstRootPathFinder(this, targetObj);
            pathFinder.OnlyOnePathPerRoot = true; // TODO Make configurable
            pathFinder.MaxResults = 10;           // TODO Make configurable
            pathFinder.FindPaths();
            return pathFinder.Paths;
        }

        public IEnumerable<ulong> FindRefs(ulong targetObj)
        {
            var result = new HashSet<ulong>();

            int targetChunk = _startOfChunkToChunkId[StartOfChunk(targetObj)];
            HashSet<int> referencingChunks;
            if (!_chunkToReferencingChunks.TryGetValue(targetChunk, out referencingChunks))
            {
                return result;
            }

            foreach (var chunkId in referencingChunks)
            {
                ulong firstObj = _chunkIdToFirstNonFreeObjectInChunk[chunkId];
                for (ulong current = firstObj;
                    current != 0 && current < firstObj + (ChunkSize - firstObj % ChunkSize);
                    current = _heap.NextObject(current))
                {
                    var type = _heap.GetObjectType(current);
                    if (type == null || type.IsFree)
                        continue;

                    type.EnumerateRefsOfObject(current, (child, _) =>
                    {
                        if (child == targetObj)
                        {
                            result.Add(current);
                        }
                    });
                }
            }
            return result;
        }

        private void DisplayStatistics()
        {
            _context.WriteLine("Total chunks: {0}, Chunk size: {1}", _chunkIdToFirstNonFreeObjectInChunk.Count, ChunkSize);

            ulong totalHeapBytes = (ulong)_heap.Segments.Sum(s => (long)(s.End - s.Start));
            _context.WriteLine("Memory covered by all segments: {0} bytes", totalHeapBytes);
            _context.WriteLine("Memory covered by all chunks:   {0} bytes", _chunkIdToFirstNonFreeObjectInChunk.Count * ChunkSize);
        }

        private int ChunkIdForObject(ulong objAddress)
        {
            // It turns out that heap enumeration segment-by-segment (as in BuildChunks) can actually
            // miss objects. In the dump C:\temp\w3wpclean.dmp, the object 8671404256 exists and GetObjectType
            // returns a valid value (and it is also reachable through some root), but it is not returned
            // by EnumerateObjects or by traversing segments manually. So, we add the missing chunks on the
            // fly while enumerating objects.

            int chunkId;
            ulong startOfChunk = StartOfChunk(objAddress);
            if (!_startOfChunkToChunkId.TryGetValue(startOfChunk, out chunkId))
            {
                ++_objectsWithMissingChunkIds;

                chunkId = _lastChunkId;
                ++_lastChunkId;
                _startOfChunkToChunkId.Add(startOfChunk, chunkId);

                // Find the first non-free object in this chunk. It isn't necessarily the object we are now
                // looking at, unfortunately. We might have multiple objects with missing chunks, and the first
                // we see isn't necessarily the one with the lowest address.
                for (ulong current = startOfChunk; current <= objAddress; current += 4)
                {
                    var type = _heap.GetObjectType(current);
                    if (type != null && !type.IsFree)
                    {
                        _chunkIdToFirstNonFreeObjectInChunk.Add(chunkId, current);
                        break;
                    }
                }
            }
            return chunkId;
        }

        private void BuildChunkIndex()
        {
            var seen = new ObjectSet(_heap);
            var evalStack = new Stack<ulong>();
            foreach (var root in _heap.EnumerateRoots(enumerateStatics: false))
            {
                evalStack.Push(root.Object);
                _directlyRooted.Add(root.Object);
            }
            while (evalStack.Count > 0)
            {
                ulong parent = evalStack.Pop();
                if (seen.Contains(parent))
                    continue;

                seen.Add(parent);

                var type = _heap.GetObjectType(parent);
                if (type == null || type.IsFree || String.IsNullOrEmpty(type.Name))
                    continue;

                type.EnumerateRefsOfObject(parent, (child, _) =>
                {
                    // Due to possibly a corruption or a bug in ClrMD, the inner fields might
                    // occasionally fall outside of the heap, or point to objects whose type
                    // cannot be obtained. We don't have a choice but to ignore these.
                    if (_heap.GetObjectType(child) == null)
                        return;

                    int childChunk = ChunkIdForObject(child);
                    int parentChunk = ChunkIdForObject(parent);
                    HashSet<int> referencingChunks;
                    if (!_chunkToReferencingChunks.TryGetValue(childChunk, out referencingChunks))
                    {
                        referencingChunks = new HashSet<int>();
                        _chunkToReferencingChunks.Add(childChunk, referencingChunks);
                    }
                    referencingChunks.Add(parentChunk);

                    if (child != 0 && !seen.Contains(child))
                    {
                        evalStack.Push(child);
                    }
                });
            }

            _context.WriteLine("Average referencing chunks per chunk: {0:N}",
                (from list in _chunkToReferencingChunks.Values select list.Count).Average());
            _context.WriteLine("Max referencing chunks per chunk:     {0}",
                (from list in _chunkToReferencingChunks.Values select list.Count).Max());
            _context.WriteLine("Min referencing chunks per chunk:     {0}",
                (from list in _chunkToReferencingChunks.Values select list.Count).Min());
            _context.WriteLine("Objects with missing chunk ids:       {0}", _objectsWithMissingChunkIds);
        }

        private void BuildChunks()
        {
            foreach (var segment in _heap.Segments)
            {
                ulong nextObject = segment.EnumerateObjects().FirstOrDefault(obj => !_heap.GetObjectType(obj).IsFree);
                while (nextObject != 0)
                {
                    ulong startOfChunk = StartOfChunk(nextObject);

                    _chunkIdToFirstNonFreeObjectInChunk.Add(_lastChunkId, nextObject);
                    _startOfChunkToChunkId.Add(startOfChunk, _lastChunkId);
                    ++_lastChunkId;

                    ulong nextChunkStart = startOfChunk + ChunkSize;

                    for (;
                        nextObject != 0 && (nextObject < nextChunkStart || _heap.GetObjectType(nextObject).IsFree);
                        nextObject = segment.NextObject(nextObject)) ;
                }
            }
        }

        private static ulong StartOfChunk(ulong address)
        {
            return address - (address % ChunkSize);
        }
    }
}
