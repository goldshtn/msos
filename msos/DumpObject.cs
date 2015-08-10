using CmdLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.LiveProcess)]
    [Verb("!DumpObj", HelpText = "Display object contents.")]
    [Verb("!do", HelpText = "Display object contents.")]
    class DumpObject : ICommand
    {
        [Value(0, Required = true, Hexadecimal = true)]
        public ulong ObjectAddress { get; set; }

        [Option("nofields", HelpText = "Do not display the object's fields.")]
        public bool NoFields { get; set; }

        [Option("norecurse", HelpText = "Do not display the fields of embedded value types recursively.")]
        public bool NoRecurse { get; set; }

        [Option("type", HelpText =
            "The exact type name of the object at the specified address. Required for " +
            "value types, which do not have embedded type information.")]
        public string TypeName { get; set; }

        private CommandExecutionContext _context;

        public void Execute(CommandExecutionContext context)
        {
            _context = context;

            ClrType type;
            if (!String.IsNullOrEmpty(TypeName))
            {
                type = context.Heap.GetTypeByName(TypeName);
                if (type == null)
                {
                    context.WriteError("There is no type named '{0}'.", TypeName);
                    return;
                }
            }
            else
            {
                type = context.Heap.GetObjectType(ObjectAddress);
                if (type == null || String.IsNullOrEmpty(type.Name))
                {
                    context.WriteError("The specified address is not an object.");
                    return;
                }
            }

            ulong mt = 0;
            if (type.IsObjectReference && !context.Runtime.ReadPointer(ObjectAddress, out mt))
            {
                context.WriteWarning("Unable to retrieve MT for object.");
            }

            var size = type.GetSize(ObjectAddress);

            context.WriteLine("Name:     {0}", type.Name);
            if (mt != 0)
            {
                context.WriteLine("MT:       {0:x16}", mt);
            }
            context.WriteLine("Size:     {0}(0x{1:x}) bytes", size, size);
            if (type.IsArray)
            {
                context.WriteLine("Array:    size {0}, element type {1}",
                    type.GetArrayLength(ObjectAddress),
                    type.ArrayComponentType != null ? type.ArrayComponentType.Name : "<unknown");
            }
            context.WriteLine("Assembly: {0}", type.Module.FileName);
            if (type.HasSimpleValue)
            {
                context.WriteLine("Value:    {0}", type.GetValue(ObjectAddress));
            }

            if (!NoFields && type.Fields.Count > 0)
            {
                context.WriteLine("Fields:");
                context.WriteLine("{0,-8} {1,-20} {2,-3} {3,-10} {4,-20} {5}",
                    "Offset", "Type", "VT", "Attr", "Value", "Name");
                foreach (var field in type.Fields)
                {
                    DisplayFieldRecursively(field, new InstanceFieldValueForDisplayRetriever(ObjectAddress), field.Offset, depth: type.IsValueClass ? 1 : 0);
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
            if (NoRecurse && field.ElementType == ClrElementType.Struct)
            {
                _context.WriteLink("", String.Format("!do {0:x16} --type {1}",
                    address, field.Type.Name));
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
}
