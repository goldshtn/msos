using CommandLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!bhi", HelpText =
        "Builds a heap index that can be used for efficient object queries and stores it to the specified file. " +
        "Use the --nofile switch to store the index in memory only, if you do not plan to reuse it.")]
    class BuildHeapIndex : ICommand
    {
        [Option('f', HelpText = "The file name in which to store the index. Load later with !lhi.")]
        public string HeapIndexFileName { get; set; }

        [Option("nofile", Default = false, HelpText = "Store the index in memory only.")]
        public bool InMemoryOnly { get; set; }

        [Option("fast", Default = false, HelpText = 
            "In large dumps, enumerating all the static roots in detail can take time. " +
            "Use this switch to make root enumeration faster at the expense of not knowing "+
            "the precise name of a static variable rooting your objects.")]
        public bool EnumerateRootsFast { get; set; }

        [Option("chunkSize", Default = 1024, HelpText =
            "The chunk size to use when segmenting the heap. The heap index stores reference information " +
            "at the chunk level. A smaller chunk size means a larger index but faster command response times.")]
        public int ChunkSize { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            if (!InMemoryOnly && String.IsNullOrEmpty(HeapIndexFileName))
            {
                context.WriteError("You must either request an in-memory index (--nofile), or a file name to store it (-f).");
                return;
            }
            if (InMemoryOnly && !String.IsNullOrEmpty(HeapIndexFileName))
            {
                context.WriteError("The --nofile and -f options are incompatible.");
                return;
            }

            // If the index fails to build, clear it so that other commands know we don't have an index.
            context.HeapIndex = new HeapIndex(context);
            if (!context.HeapIndex.Build(ChunkSize, InMemoryOnly ? null : HeapIndexFileName, !EnumerateRootsFast))
                context.HeapIndex = null;
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
        [Option('f', Required = true, HelpText = "The file name from which to load the index.")]
        public string HeapIndexFileName { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            context.HeapIndex = new HeapIndex(context);
            context.HeapIndex.Load(HeapIndexFileName);
        }
    }

    class HeapIndex
    {
        private int _chunkSize;
        private List<ulong> _chunkIdToFirstNonFreeObjectInChunk = new List<ulong>();
        private Dictionary<ulong, int> _startOfChunkToChunkId = new Dictionary<ulong, int>();
        private Dictionary<int, HashSet<int>> _tempChunkToReferencingChunks = new Dictionary<int, HashSet<int>>();
        private Dictionary<int, int[]> _chunkToReferencingChunks = new Dictionary<int, int[]>();
        
        private int _objectsWithMissingChunkIds = 0;
        private int _lastChunkId = 0;
        
        private HashSet<ulong> _directlyRooted = new HashSet<ulong>();
        private List<SimplifiedRoot> _allRoots;
        private bool _staticRootsEnumerated;

        private CommandExecutionContext _context;
        private ClrHeap _heap;

        public HeapIndex(CommandExecutionContext context)
        {
            _context = context;
            _heap = context.Runtime.GetHeap();
        }

        public bool Build(int chunkSize, string indexFileName, bool enumerateAllRoots)
        {
            if (chunkSize < 256 || chunkSize > 1048576 || chunkSize % 16 != 0)
            {
                _context.WriteError("Chunk size must be between 256 bytes and 1MB, and must be a multiple of 16.");
                return false;
            }

            _chunkSize = chunkSize;
            _staticRootsEnumerated = enumerateAllRoots;
            Measure(() =>
            {
                _allRoots = (from root in _heap.EnumerateRoots(enumerateStatics: enumerateAllRoots)
                             select new SimplifiedRoot(root)).ToList();
            }, "Enumerating roots");

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

            if (!String.IsNullOrEmpty(indexFileName))
            {
                Measure(() => Save(indexFileName), "Saving index to disk");
            }
            else
            {
                _context.WriteWarning("You did not specify a file name, so the index will be stored only in memory. " +
                    "If you plan to perform further analysis in another session, it is recommended that you store " +
                    "the index to disk and later load it using the !lhi command.");
            }

            return true;
        }

        [Serializable]
        class Indexes
        {
            public List<ulong> ChunkIdToFirstNonFreeObjectInChunk;
            public Dictionary<ulong, int> StartOfChunkToChunkId;
            public Dictionary<int, int[]> ChunkToReferencingChunks;
            public ulong[] DirectlyRooted;
            public List<SimplifiedRoot> AllRoots;
            public bool StaticRootsEnumerated;
            public int ChunkSize;
        }

        [Serializable]
        public class SimplifiedRoot
        {
            public ulong Address;
            public ulong Object;
            public GCRootKind Kind;
            public string DisplayText;

            public SimplifiedRoot(ClrRoot root)
            {
                Address = root.Address;
                Object = root.Object;
                Kind = root.Kind;
                DisplayText = root.BetterToString();
            }
        }

        public void Load(string indexFileName)
        {
            using (var file = File.OpenRead(indexFileName))
            using (var decompressor = new ICSharpCode.SharpZipLib.BZip2.BZip2InputStream(file))
            {
                var serializer = new NetSerializer.Serializer(new Type[] { typeof(Indexes) });
                Indexes indexes = (Indexes)serializer.Deserialize(decompressor);
                _chunkIdToFirstNonFreeObjectInChunk = indexes.ChunkIdToFirstNonFreeObjectInChunk;
                _startOfChunkToChunkId = indexes.StartOfChunkToChunkId;
                _chunkToReferencingChunks = indexes.ChunkToReferencingChunks;
                _directlyRooted = new HashSet<ulong>(indexes.DirectlyRooted);
                _staticRootsEnumerated = indexes.StaticRootsEnumerated;
                _allRoots = indexes.AllRoots;
                _chunkSize = indexes.ChunkSize;
            }

            if (!_staticRootsEnumerated)
            {
                _context.WriteWarning("This heap index does not have detailed static root information. " +
                    "As a result, you will not see the names of static variables referencing your objects, " +
                    "only their addresses. Recreate the index without the --fast switch to get full " +
                    "information. This may be slower.");
            }
        }

        private void Save(string indexFileName)
        {
            using (var file = File.Create(indexFileName))
            using (var compressor = new ICSharpCode.SharpZipLib.BZip2.BZip2OutputStream(file))
            {
                var serializer = new NetSerializer.Serializer(new Type[] { typeof(Indexes) });
                var indexes = new Indexes();
                indexes.ChunkIdToFirstNonFreeObjectInChunk = _chunkIdToFirstNonFreeObjectInChunk;
                indexes.StartOfChunkToChunkId = _startOfChunkToChunkId;
                indexes.ChunkToReferencingChunks = _chunkToReferencingChunks;
                indexes.DirectlyRooted = _directlyRooted.ToArray();
                indexes.AllRoots = _allRoots;
                indexes.StaticRootsEnumerated = _staticRootsEnumerated;
                indexes.ChunkSize = _chunkSize;
                serializer.Serialize(compressor, indexes);
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
            public SimplifiedRoot Root;
            public ulong[] Chain;
        }

        abstract class RootPathFinder
        {
            public int MaxLocalRoots { get; set; }
            public int MaxResults { get; set; }
            public int MaxDepth { get; set; }

            protected HeapIndex Index { get; private set; }
            protected ObjectSet Visited { get; private set; }
            protected ulong TargetObject { get; private set; }
            protected bool Stop { get; private set; }
            
            private List<RootPath> _paths = new List<RootPath>();
            private Dictionary<ulong, HashSet<SimplifiedRoot>> _rootsSeenReferencingObject = new Dictionary<ulong, HashSet<SimplifiedRoot>>();
            private int _localRootsSeen = 0;

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
                        HashSet<SimplifiedRoot> seenRoots;
                        if (!_rootsSeenReferencingObject.TryGetValue(obj, out seenRoots))
                        {
                            seenRoots = new HashSet<SimplifiedRoot>();
                            _rootsSeenReferencingObject.Add(obj, seenRoots);
                        }

                        // Limit the total number of local roots displayed. There could be thousands
                        // of different local variables leading to our target object through a variety
                        // of other objects. After seeing a few of those, the value of the information
                        // deteriorates.
                        if (root.Kind == GCRootKind.LocalVar && _localRootsSeen >= MaxLocalRoots)
                            continue;

                        // Only accept one local root per object. There can be literally thousands of
                        // local variables pointing to each object, and it's unnecessary garbage to display
                        // them all. Also, obviously don't accept the same root more than once.
                        if (seenRoots.Contains(root) ||
                            (root.Kind == GCRootKind.LocalVar && seenRoots.Any(r => r.Object == obj)))
                        {
                            continue;
                        }

                        seenRoots.Add(root);
                        if (root.Kind == GCRootKind.LocalVar)
                            ++_localRootsSeen;

                        AddPath(new RootPath { Root = root, Chain = chainArray });
                        
                        // If the total number of paths displayed exceed the limit, stop completely.
                        // Subclasses should stop calling us once we set Stop=true.
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
                if (Stop || path.Count > MaxDepth)
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
                    if (chain.Length > MaxDepth)
                        continue;

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

        class ParallelBreadthFirstRootPathFinder : RootPathFinder
        {
            public ParallelBreadthFirstRootPathFinder(HeapIndex index, ulong targetObject)
                : base(index, targetObject)
            {
            }

            public override void FindPaths()
            {
                var evalQueue = new ConcurrentQueue<ulong[]>();
                evalQueue.Enqueue(new ulong[] { TargetObject });
                while (evalQueue.Count > 0)
                {
                    if (Stop)
                        break;

                    // Level-synchronized BFS: process each frontier in parallel
                    Parallel.ForEach(evalQueue, chain =>
                    {
                        if (chain.Length > MaxDepth)
                            return;

                        var current = chain[0];

                        // TODO Check if it's necessary to reduce synchronization around 'Visited'
                        lock (Visited)
                        {
                            if (Visited.Contains(current))
                                return;

                            Visited.Add(current);
                        }

                        lock (this)
                        {
                            AddPathIfRootReached(current, chain);
                        }

                        // Turns out ClrMD's heap operations are not thread-safe because the underlying
                        // memory data reader is not thread-safe (WAT?!?). So we have to lock here around
                        // the heap index operations, and that slows everything down to a crawl because
                        // there are some large objects where EnumerateRefsOfObject takes >1000ms.
                        // Throw in one or two of those, and the parallel version runs much slower than
                        // the sequential one.
                        // TODO See if this can be improved, filed an issue on https://github.com/Microsoft/dotnetsamples/issues/21
                        List<ulong> refs;
                        lock (Index)
                        {
                            refs = Index.FindRefs(current).ToList();
                        }

                        Parallel.ForEach(refs, referencingObj =>
                        {
                            var newChain = new ulong[chain.Length + 1];
                            newChain[0] = referencingObj;
                            Array.Copy(chain, 0, newChain, 1, chain.Length);
                            evalQueue.Enqueue(newChain);
                        });
                    });
                }
            }
        }

        public IEnumerable<RootPath> FindPaths(ulong targetObj, int maxResults, int maxLocalRoots, int maxDepth, bool runInParallel)
        {
            RootPathFinder pathFinder;
            if (runInParallel)
            {
                pathFinder = new ParallelBreadthFirstRootPathFinder(this, targetObj);
            }
            else
            {
                pathFinder = new BreadthFirstRootPathFinder(this, targetObj);
            }
            pathFinder.MaxResults = maxResults;
            pathFinder.MaxLocalRoots = maxLocalRoots;
            pathFinder.MaxDepth = maxDepth;
            pathFinder.FindPaths();
            return pathFinder.Paths;
        }

        public IEnumerable<ulong> FindRefs(ulong targetObj)
        {
            var result = new HashSet<ulong>();

            int targetChunk = _startOfChunkToChunkId[StartOfChunk(targetObj)];
            int[] referencingChunks;
            if (!_chunkToReferencingChunks.TryGetValue(targetChunk, out referencingChunks))
            {
                return result;
            }

            foreach (var chunkId in referencingChunks)
            {
                ulong firstObj = _chunkIdToFirstNonFreeObjectInChunk[chunkId];
                for (ulong current = firstObj;
                    current != 0 && current < firstObj + ((ulong)_chunkSize - firstObj % (ulong)_chunkSize);
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
            _context.WriteLine("Total chunks: {0}, Chunk size: {1}", _chunkIdToFirstNonFreeObjectInChunk.Count, ((ulong)_chunkSize).ToMemoryUnits());

            ulong totalHeapBytes = (ulong)_heap.Segments.Sum(s => (long)(s.End - s.Start));
            _context.WriteLine("Memory covered by all segments: {0}", totalHeapBytes.ToMemoryUnits());
            _context.WriteLine("Memory covered by all chunks:   {0}", ((ulong)(_chunkIdToFirstNonFreeObjectInChunk.Count * _chunkSize)).ToMemoryUnits());
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
                        _chunkIdToFirstNonFreeObjectInChunk.Add(current);
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
                    if (!_tempChunkToReferencingChunks.TryGetValue(childChunk, out referencingChunks))
                    {
                        referencingChunks = new HashSet<int>();
                        _tempChunkToReferencingChunks.Add(childChunk, referencingChunks);
                    }
                    referencingChunks.Add(parentChunk);

                    if (child != 0 && !seen.Contains(child))
                    {
                        evalStack.Push(child);
                    }
                });
            }

            _context.WriteLine("Average referencing chunks per chunk: {0:N}",
                (from list in _tempChunkToReferencingChunks.Values select list.Count).Average());
            _context.WriteLine("Max referencing chunks per chunk:     {0}",
                (from list in _tempChunkToReferencingChunks.Values select list.Count).Max());
            _context.WriteLine("Min referencing chunks per chunk:     {0}",
                (from list in _tempChunkToReferencingChunks.Values select list.Count).Min());
            _context.WriteLine("Objects with missing chunk ids:       {0}", _objectsWithMissingChunkIds);

            // Convert to a more compact representation for in-memory use and serialization.
            foreach (var kvp in _tempChunkToReferencingChunks)
            {
                _chunkToReferencingChunks.Add(kvp.Key, kvp.Value.ToArray());
            }
            _tempChunkToReferencingChunks.Clear();
        }

        private void BuildChunks()
        {
            foreach (var segment in _heap.Segments)
            {
                ulong nextObject = segment.EnumerateObjects().FirstOrDefault(obj => !_heap.GetObjectType(obj).IsFree);
                while (nextObject != 0)
                {
                    ulong startOfChunk = StartOfChunk(nextObject);

                    _chunkIdToFirstNonFreeObjectInChunk.Add(nextObject);
                    _startOfChunkToChunkId.Add(startOfChunk, _lastChunkId);
                    ++_lastChunkId;

                    ulong nextChunkStart = startOfChunk + (ulong)_chunkSize;

                    for (;
                        nextObject != 0 && (nextObject < nextChunkStart || _heap.GetObjectType(nextObject).IsFree);
                        nextObject = segment.NextObject(nextObject)) ;
                }
            }
        }

        private ulong StartOfChunk(ulong address)
        {
            return address - (address % (ulong)_chunkSize);
        }
    }
}
