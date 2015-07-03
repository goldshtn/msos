using CommandLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    // Portions of the code below adapted from https://github.com/Microsoft/dotnetsamples/blob/master/Microsoft.Diagnostics.Runtime/CLRMD/GCRoot/Program.cs
    // under the MIT License
    [Verb("!GCRoot", HelpText = "Display paths from GC roots leading to the specified object.")]
    class GCRoot : ICommand
    {
        [Value(0, Required = true)]
        public string ObjectAddress { get; set; }

        private HashSet<ClrRoot> _visitedRoots = new HashSet<ClrRoot>();
        private Dictionary<ulong, List<ClrRoot>> _rootsByObject = new Dictionary<ulong, List<ClrRoot>>();
        private List<ClrRoot> _roots;
        private Dictionary<ulong, Node> _targets = new Dictionary<ulong, Node>();
        private ObjectSet _visitedObjects;
        private ClrHeap _heap;
        private CommandExecutionContext _context;

        public void Execute(CommandExecutionContext context)
        {
            _context = context;

            ulong objPtr;
            if (!ulong.TryParse(ObjectAddress, NumberStyles.HexNumber, null, out objPtr))
            {
                context.WriteError("The specified object address format is invalid.");
                return;
            }

            _heap = context.Runtime.GetHeap();
            if (!_heap.CanWalkHeap)
            {
                context.WriteError("The heap is not in a walkable state.");
                return;
            }

            var type = _heap.GetObjectType(objPtr);
            if (type == null)
            {
                context.WriteError("The specified address does not point to a valid object.");
                return;
            }

            _visitedObjects = new ObjectSet(_heap);
            _targets[objPtr] = new Node(objPtr, type);
            FillRootDictionary();

            foreach (var root in _roots)
            {
                if (_visitedRoots.Contains(root) || _visitedObjects.Contains(root.Object))
                    continue;

                Node path = TryFindPathToTarget(root);
                if (path != null)
                    PrintOnePath(path);
            }
        }

        private Node TryFindPathToTarget(ClrRoot root)
        {
            ClrType type = _heap.GetObjectType(root.Object);
            if (type == null)
                return null;

            List<ulong> refList = new List<ulong>();
            List<int> offsetList = new List<int>();

            Node curr = new Node(root.Object, type);
            while (curr != null)
            {
                if (curr.Children == null)
                {
                    refList.Clear();
                    offsetList.Clear();

                    curr.Type.EnumerateRefsOfObject(curr.Object, (child, offset) =>
                    {
                        if (child != 0)
                        {
                            refList.Add(child);
                            offsetList.Add(offset);
                        }
                    });

                    curr.Children = refList.ToArray();
                    curr.Offsets = offsetList.ToArray();
                }
                else
                {
                    if (curr.Curr < curr.Children.Length)
                    {
                        ulong nextObj = curr.Children[curr.Curr];
                        int offset = curr.Offsets[curr.Curr];
                        curr.Curr++;

                        if (_visitedObjects.Contains(nextObj))
                            continue;

                        _visitedObjects.Add(nextObj);

                        Node next = null;
                        if (_targets.TryGetValue(nextObj, out next))
                        {
                            curr.Next = next;
                            next.Prev = curr;
                            next.Offset = offset;

                            while (curr.Prev != null)
                            {
                                _targets[curr.Object] = curr;
                                curr = curr.Prev;
                            }

                            _targets[curr.Object] = curr;
                            return curr;
                        }

                        type = _heap.GetObjectType(nextObj);
                        if (type != null && type.ContainsPointers)
                        {
                            curr = new Node(nextObj, type, curr);
                            curr.Offset = offset;
                        }
                    }
                    else
                    {
                        curr = curr.Prev;

                        if (curr != null)
                            curr.Next = null;
                    }
                }
            }

            return null;
        }

        private void PrintOnePath(Node path)
        {
            for (var node = path; node != null; node = node.Next)
            {
                List<ClrRoot> roots;
                if (_rootsByObject.TryGetValue(node.Object, out roots))
                {
                    foreach (var root in roots)
                    {
                        _visitedRoots.Add(root);
                        _context.WriteLine("{0:X16} -> {1:X16} {2}", root.Address, root.Object, root.Name);
                    }
                }
            }

            for (var node = path; node != null; node = node.Next)
            {
                _context.WriteLine("\t-> {0:X16} {1}", node.Object, node.Type.Name);
            }
            _context.WriteLine("");
        }

        // An object may be pointed to by multiple roots. Construct a dictionary that says,
        // given an object, which roots point to that object.
        private void FillRootDictionary()
        {
            _roots = new List<ClrRoot>(_heap.EnumerateRoots());
            foreach (var root in _roots)
            {
                List<ClrRoot> list;
                if (!_rootsByObject.TryGetValue(root.Object, out list))
                {
                    list = new List<ClrRoot>();
                    _rootsByObject[root.Object] = list;
                }

                list.Add(root);
            }
        }

        class Node
        {
            public Node Next;
            public Node Prev;
            public ulong Object;
            public ClrType Type;
            public int Offset;
            public ulong[] Children;
            public int[] Offsets;
            public int Curr;

            public Node(ulong obj, ClrType type, Node prev = null)
            {
                Object = obj;
                Prev = prev;
                Type = type;

                if (type == null)
                    throw new ArgumentNullException("type");

                if (prev != null)
                    prev.Next = this;
            }
        }
    }
}
