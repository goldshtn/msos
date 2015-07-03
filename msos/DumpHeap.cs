using CommandLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!DumpHeap", HelpText = "List heap objects and statistics.")]
    class DumpHeap : ICommand
    {
        [Option("type", HelpText = "A regular expression specifying the types of objects to display.")]
        public string TypeRegex { get; set; }

        [Option("mt", HelpText="A method table address specifying the objects to display.")]
        public string MethodTable { get; set; }

        [Option("stat", HelpText="Display only type statistics and not individual objects.")]
        public bool StatisticsOnly { get; set; }

        // TODO --min, --max

        struct TypeInfo
        {
            public ulong Count;
            public ulong Size;
            public string TypeName;
        }

        private ClrHeap _heap;
        private ulong _methodTable;

        private bool FilterObject(ulong obj, ulong mt)
        {
            bool match = true;
            ClrType type = null;

            if (!String.IsNullOrEmpty(TypeRegex))
            {
                type = _heap.GetObjectType(obj);
                if (type == null || String.IsNullOrEmpty(type.Name))
                    match = false;

                if (match)
                {
                    // TODO Consider compiling the regex
                    match = Regex.IsMatch(type.Name, TypeRegex);
                }
            }

            if (match && !String.IsNullOrEmpty(MethodTable))
            {
                match = _methodTable == mt;
            }

            return match;
        }

        public void Execute(CommandExecutionContext context)
        {
            if (!String.IsNullOrEmpty(MethodTable))
            {
                if (!ulong.TryParse(MethodTable, System.Globalization.NumberStyles.HexNumber, null, out _methodTable))
                {
                    context.WriteError("Method table provided is not in a valid format.");
                    return;
                }
            }
            if (!String.IsNullOrEmpty(TypeRegex))
            {
                try
                {
                    new Regex(TypeRegex);
                }
                catch (ArgumentException)
                {
                    context.WriteError("The regular expression specified for --type is not valid; did you forget to escape regex characters?");
                    return;
                }
            }

            _heap = context.Runtime.GetHeap();
            if (!_heap.CanWalkHeap)
            {
                context.WriteError("The heap is not in a walkable state.");
                return;
            }

            var typeInfos = new Dictionary<ulong, TypeInfo>(); // MT to TypeInfo
            long totalObjectCount = 0;
            if (!StatisticsOnly)
            {
                context.WriteLine("{0,-20} {1,-20} {2}", "MT", "Address", "Size");
            }
            foreach (var obj in _heap.EnumerateObjects())
            {
                ulong mt = 0;
                context.Runtime.ReadPointer(obj, out mt);

                if (!FilterObject(obj, mt))
                    continue;

                var type = _heap.GetObjectType(obj);
                var size = type.GetSize(obj);

                if (!StatisticsOnly)
                {
                    context.WriteLine("{0,-20:x16} {1,-20:x16} {2,-10}", mt, obj, size);
                }
                if (typeInfos.ContainsKey(mt))
                {
                    var current = typeInfos[mt];
                    current.Count += 1;
                    current.Size += size;
                    typeInfos[mt] = current;
                }
                else
                {
                    var objType = _heap.GetObjectType(obj);
                    var objTypeName = objType != null ? objType.Name : "<no name>";
                    typeInfos.Add(mt, new TypeInfo { Size = size, Count = 1, TypeName = objTypeName });
                }
                ++totalObjectCount;
            }

            context.WriteLine("Statistics:");
            context.WriteLine("{0,-20} {1,-10} {2,-10} {3}", "MT", "Count", "TotalSize", "Class Name");
            foreach (var kvp in (from e in typeInfos orderby e.Value.Size ascending select e))
            {
                context.WriteLine("{0,-20:x16} {1,-10} {2,-10} {3}",
                    kvp.Key, kvp.Value.Count, kvp.Value.Size, kvp.Value.TypeName);
            }
            context.WriteLine("Total {0} objects", totalObjectCount);
        }
    }
}
