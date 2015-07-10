using CommandLine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!ObjSize", HelpText = "Display the size of the object graph referenced by the specified object, and including it.")]
    class ObjSize : ICommand
    {
        [Value(0, Required = true)]
        public string ObjectAddress { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            ulong objPtr;
            if (!CommandHelpers.ParseAndVerifyValidObjectAddress(context, ObjectAddress, out objPtr))
                return;

            var toGoThrough = new Stack<ulong>();
            toGoThrough.Push(objPtr);
            var seen = new ObjectSet(context.Heap);

            ulong count = 0, size = 0;
            while (toGoThrough.Count > 0)
            {
                var obj = toGoThrough.Pop();
                if (seen.Contains(obj))
                    continue;

                seen.Add(obj);

                var type = context.Heap.GetObjectType(obj);
                if (type == null || type.IsFree || String.IsNullOrEmpty(type.Name))
                    continue;

                ++count;
                size += type.GetSize(obj);

                type.EnumerateRefsOfObject(obj, (child, _) =>
                {
                    if (child != 0 && !seen.Contains(child))
                    {
                        toGoThrough.Push(child);
                    }
                });
            }

            context.WriteLine("{0:x16} graph size is {1} objects, {2} bytes", objPtr, count, size);
        }
    }
}
