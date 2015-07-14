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
    [Verb("!DumpObj", HelpText = "Display object contents.")]
    class DumpObject : ICommand
    {
        [Value(0, Required = true)]
        public string ObjectAddress { get; set; }

        [Option("nofields", HelpText = "Do not display the object's fields.")]
        public bool NoFields { get; set; }

        [Option("norecurse", HelpText = "Do not display the fields of embedded value types recursively.")]
        public bool NoRecurse { get; set; }

        private CommandExecutionContext _context;

        public void Execute(CommandExecutionContext context)
        {
            _context = context;

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
                    DisplayFieldRecursively(field, new InstanceFieldValueForDisplayRetriever(objPtr), field.Offset);
                }
                foreach (var field in type.ThreadStaticFields)
                {
                    context.WriteLine("{0,-8:x} {1,-20} {2,-3} {3,-10} {4,-20:x16} {5}",
                        field.Offset, field.GetFieldTypeNameTrimmed(),
                        (field.IsPrimitive() || field.IsValueClass()) ? 1 : 0,
                        "shared", "thrstatic", field.Name);
                    foreach (var appDomain in context.Runtime.AppDomains)
                    {
                        foreach (var thread in context.Runtime.Threads)
                        {
                            DisplayFieldRecursively(field, new ThreadStaticFieldValueForDisplayRetriever(appDomain, thread), field.Offset);
                        }
                    }
                }
                foreach (var field in type.StaticFields)
                {
                    context.WriteLine("{0,-8:x} {1,-20} {2,-3} {3,-10} {4,-20:x16} {5}",
                        field.Offset, field.GetFieldTypeNameTrimmed(),
                        (field.IsPrimitive() || field.IsValueClass()) ? 1 : 0,
                        "shared", "static", field.Name);
                    foreach (var appDomain in context.Runtime.AppDomains)
                    {
                        DisplayFieldRecursively(field, new StaticFieldValueForDisplayRetriever(appDomain), field.Offset);
                    }
                }
            }
        }

        private void DisplayFieldRecursively<TField>(TField field, IFieldValueForDisplayRetriever<TField> retriever, int offset, string baseName = "", int depth = 0)
            where TField : ClrField
        {
            bool inner = depth > 0;
            var address = retriever.GetFieldAddress(field, inner);
            
            _context.Write(retriever.GetDisplayString(field, offset, baseName, inner));
            if (field.ElementType == ClrElementType.Object)
            {
                _context.WriteLink("", String.Format("!do {0}", retriever.GetFieldValue(field, inner)));
            }
            _context.WriteLine();

            if (!NoRecurse && field.ElementType == ClrElementType.Struct)
            {
                foreach (var innerField in field.Type.Fields)
                {
                    var innerRetriever = new InstanceFieldValueForDisplayRetriever(address);
                    DisplayFieldRecursively(innerField, innerRetriever, offset + innerField.Offset, baseName + field.Name + ".", depth + 1);
                }
            }
        }
    }

    [Verb("!do", HelpText = "Display object contents.")]
    class DO : DumpObject
    {
    }
}
