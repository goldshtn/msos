using CommandLine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!DumpObj", HelpText = "Display object contents.")]
    class DumpObject : ICommand
    {
        [Value(0, Required = true)]
        public string ObjectAddress { get; set; }

        [Option("nofields", HelpText = "Do not display the object's fields.")]
        public bool NoFields { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            ulong objPtr;
            if (!ulong.TryParse(ObjectAddress, NumberStyles.HexNumber, null, out objPtr))
            {
                context.WriteError("Invalid object address format specified.");
                return;
            }

            var heap = context.Runtime.GetHeap();
            var type = heap.GetObjectType(objPtr);
            if (type == null || String.IsNullOrEmpty(type.Name))
            {
                context.WriteError("The specified address is not an object.");
                return;
            }

            ulong mt;
            if (!context.Runtime.ReadPointer(objPtr, out mt))
            {
                context.WriteError("Unable to retrieve MT for object.");
                return;
            }

            var size = type.GetSize(objPtr);

            context.WriteLine("Name:     {0}", type.Name);
            context.WriteLine("MT:       {0:x16}", mt);
            context.WriteLine("Size:     {0}(0x{1:x}) bytes", size, size);
            context.WriteLine("Assembly: {0}", type.Module.FileName);
            if (type.HasSimpleValue)
            {
                context.WriteLine("Value:    {0}", type.GetValue(objPtr));
            }

            if (!NoFields)
            {
                context.WriteLine("Fields:");
                context.WriteLine("{0,-8} {1,-20} {2,-3} {3,-10} {4,-20} {5}",
                    "Offset", "Type", "VT", "Attr", "Value", "Name");
                foreach (var field in type.Fields)
                {
                    context.WriteLine("{0,-8:x} {1,-20} {2,-3} {3,-10} {4,-20:x16} {5}",
                        field.Offset, field.GetFieldTypeNameTrimmed(),
                        (field.IsPrimitive() || field.IsValueClass()) ? 1 : 0,
                        "instance", field.GetValue(objPtr), field.Name);
                }
                foreach (var field in type.ThreadStaticFields)
                {
                    context.WriteLine("{0,-8:x} {1,-20} {2,-3} {3,-10} {4,-20:x16} {5}",
                        field.Offset, field.GetFieldTypeNameTrimmed(),
                        (field.IsPrimitive() || field.IsValueClass()) ? 1 : 0,
                        "instance", "thrstatic", field.Name);
                    foreach (var appDomain in context.Runtime.AppDomains)
                    {
                        foreach (var thread in context.Runtime.Threads)
                        {
                            context.WriteLine("   >> Domain:Thread:Value  {0:x16}:{1}:{2} <<",
                                appDomain.Address, thread.ManagedThreadId,
                                field.GetValue(appDomain, thread) ?? "NotInit");
                        }
                    }
                }
                foreach (var field in type.StaticFields)
                {
                    context.WriteLine("{0,-8:x} {1,-20} {2,-3} {3,-10} {4,-20:x16} {5}",
                        field.Offset, field.GetFieldTypeNameTrimmed(),
                        (field.IsPrimitive() || field.IsValueClass()) ? 1 : 0,
                        "instance", "static", field.Name);
                    foreach (var appDomain in context.Runtime.AppDomains)
                    {
                        context.WriteLine("   >> Domain:Value  {0:x16}:{1} <<",
                            appDomain.Address, field.GetValue(appDomain) ?? "NotInit");
                    }
                }
            }
        }
    }

    [Verb("!do", HelpText = "Display object contents.")]
    class DO : DumpObject
    {
    }
}
