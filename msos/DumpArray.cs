using CommandLine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!DumpArray", HelpText = "Display all the elements in an array.")]
    class DumpArray : ICommand
    {
        [Value(0, Required = true)]
        public string ObjectAddress { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            ulong objPtr;
            if (!ulong.TryParse(ObjectAddress, NumberStyles.HexNumber, null, out objPtr))
            {
                context.WriteError("The specified object format is invalid.");
                return;
            }

            var heap = context.Runtime.GetHeap();
            var type = heap.GetObjectType(objPtr);
            if (type == null)
            {
                context.WriteError("The specified address does not point to a valid object.");
                return;
            }

            if (!type.IsArray)
            {
                context.WriteError("The object at the specified address is not an array.");
                return;
            }

            var size = type.GetSize(objPtr);
            var length = type.GetArrayLength(objPtr);
            context.WriteLine("Name:  {0}", type.Name);
            context.WriteLine("Size:  {0}(0x{1:x}) bytes", size, size);
            context.WriteLine("Array: Number of elements {0}, Type {1}",
                length, type.ArrayComponentType.Name);

            for (int i = 0; i < length; ++i)
            {
                object value;
                if (type.ArrayComponentType.IsValueClass)
                {
                    value = type.GetArrayElementAddress(objPtr, i);
                }
                else
                {
                    value = type.GetArrayElementValue(objPtr, i);
                }
                context.WriteLine("[{0}] {1:x16}", i, value ?? "<null>");
            }
        }
    }
}
