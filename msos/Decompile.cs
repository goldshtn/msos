using CommandLine;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Ast;
using Microsoft.Diagnostics.Runtime;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.LiveProcess)]
    [Verb("decompile", HelpText =
        "Decompiles the specified method, type, or module to C# code.")]
    class Decompile : ICommand
    {
        [Option("method", HelpText =
            "The method to decompile. Specify the type name separately using the --type switch.")]
        public string MethodName { get; set; }

        [Option("type", HelpText =
            "The full name (including namespace) of the type to decompile. " +
            "If no method is specified, all methods of that type are decompiled.")]
        public string TypeName { get; set; }

        [Option("assembly", HelpText =
            "The name of the assembly to decompile; e.g. System.dll." +
            "If no type name is specified, all types in that assembly are decompiled.")]
        public string AssemblyName { get; set; }

        [Option("file", HelpText =
            "The name of the file that receives the decompiled results. If no file " +
            "is specified, the decompilation output is displayed directly in the debugger.")]
        public string OutputFileName { get; set; }

        private CommandExecutionContext _context;
        private PlainTextOutput _output;

        public void Execute(CommandExecutionContext context)
        {
            _context = context;
            _output = new PlainTextOutput();

            if (!String.IsNullOrEmpty(AssemblyName))
            {
                ClrModule module = context.Runtime.EnumerateModules().SingleOrDefault(
                    m => Path.GetFileNameWithoutExtension(m.FileName).Equals(
                        Path.GetFileNameWithoutExtension(AssemblyName),
                        StringComparison.InvariantCultureIgnoreCase
                        )
                    );
                if (module == null)
                {
                    context.WriteError("Could not find the assembly '{0}'.", AssemblyName);
                    return;
                }
                if (!String.IsNullOrEmpty(TypeName))
                {
                    DecompileTypeFromModule(TypeName, module.FileName);
                }
                else
                {
                    DecompileModule(module);
                }
            }
            else if (!String.IsNullOrEmpty(TypeName))
            {
                ClrType type = context.Heap.GetTypeByName(TypeName);
                if (type == null)
                {
                    context.WriteError(
                        "Could not find the type '{0}' on the heap. Try specifying the assembly name.",
                        TypeName);
                    return;
                }
                if (!String.IsNullOrEmpty(MethodName))
                {
                    var methods = type.Methods.Where(m => m.Name == MethodName).ToArray();
                    if (methods.Length == 0)
                    {
                        context.WriteError("Could not find the method '{0}'.", MethodName);
                        return;
                    }
                    DecompileMethods(methods);
                }
                else
                {
                    DecompileType(type);
                }
            }
            else
            {
                context.WriteError("At least one of --assembly or --type must be specified.");
            }
        }

        private void DecompileTypeFromModule(string typeName, string moduleFileName)
        {
            var assemblyDef = AssemblyDefinition.ReadAssembly(moduleFileName);
            var typeDef = TypeDefFromAssemblyDef(typeName, assemblyDef);

            AstBuilder decompiler = new AstBuilder(
                new DecompilerContext(typeDef.Module));
            decompiler.AddType(typeDef);

            GenerateCode(decompiler);
        }

        private void DecompileType(ClrType type)
        {
            string moduleFileName = type.Module.FileName;
            DecompileTypeFromModule(type.Name, moduleFileName);
        }

        private void DecompileMethods(ClrMethod[] methods)
        {
            string moduleFileName = methods[0].Type.Module.FileName;
            string typeName = methods[0].Type.Name;
            
            var assemblyDef = AssemblyDefinition.ReadAssembly(moduleFileName);
            var typeDef = TypeDefFromAssemblyDef(typeName, assemblyDef);

            AstBuilder decompiler = new AstBuilder(
                new DecompilerContext(typeDef.Module) { CurrentType = typeDef });
            foreach (var method in methods)
            {
                var methodDef = typeDef.Methods.Single(
                    m => m.MetadataToken.ToUInt32() == method.MetadataToken);
                decompiler.AddMethod(methodDef);
            }

            GenerateCode(decompiler);
        }

        private static TypeDefinition TypeDefFromAssemblyDef(string typeName, AssemblyDefinition assemblyDef)
        {
            return (from moduleDef in assemblyDef.Modules
                    let t = moduleDef.GetType(typeName)
                    where t != null
                    select t).Single();
        }

        private void DecompileModule(ClrModule module)
        {
            var assemblyDef = AssemblyDefinition.ReadAssembly(module.FileName);
            AstBuilder decompiler = new AstBuilder(
                new DecompilerContext(assemblyDef.MainModule));
            decompiler.AddAssembly(assemblyDef);

            GenerateCode(decompiler);
        }

        private void GenerateCode(AstBuilder decompiler)
        {
            decompiler.GenerateCode(_output);
            if (!String.IsNullOrEmpty(OutputFileName))
            {
                File.WriteAllText(OutputFileName, _output.ToString());
            }
            else
            {
                _context.Write(_output.ToString());
            }
        }
    }
}
