using CmdLine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.LiveProcess)]
    [Verb("!DumpArray", HelpText = "Display all the elements in an array.")]
    class DumpArray : ICommand
    {
        [Value(0, Required = true, Hexadecimal = true, HelpText = "The address of the array to display.")]
        public ulong ObjectAddress { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            if (!CommandHelpers.VerifyValidObjectAddress(context, ObjectAddress))
                return;

            var type = context.Heap.GetObjectType(ObjectAddress);
            if (!type.IsArray)
            {
                context.WriteErrorLine("The object at the specified address is not an array.");
                return;
            }

            var size = type.GetSize(ObjectAddress);
            var length = type.GetArrayLength(ObjectAddress);
            context.WriteLine("Name:  {0}", type.Name);
            context.WriteLine("Size:  {0}(0x{1:x}) bytes", size, size);
            context.WriteLine("Array: Number of elements {0}, Type {1} {2}",
                length, type.ComponentType.Name,
                type.ComponentType.IsValueClass ? "(value type)" : "(reference type)");

            for (int i = 0; i < length; ++i)
            {
                context.Write("[{0}] ", i);

                object value;
                if (type.ComponentType.IsValueClass)
                {
                    value = type.GetArrayElementAddress(ObjectAddress, i);
                    if (value != null)
                    {
                        context.WriteLink(
                            String.Format("{0:x16}", value),
                            String.Format("!do {0:x16} --type {1}", value, type.ComponentType.Name)
                            );
                    }
                    else
                    {
                        context.Write("<null>");
                    }
                }
                else
                {
                    value = type.GetArrayElementValue(ObjectAddress, i);
                    ulong elementAddr = type.GetArrayElementAddress(ObjectAddress, i);
                    ulong elementRef;
                    if (context.Runtime.ReadPointer(elementAddr, out elementRef))
                    {
                        context.WriteLink(
                            String.Format("{0:x16}", value ?? "<null>"),
                            String.Format("!do {0:x16}", elementRef)
                            ); 
                    }
                    else
                    {
                        context.Write("{0:x16}", value ?? "<null>");
                    }
                }
                context.WriteLine();
            }
        }
    }
}
