using CmdLine;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;
using Microsoft.Diagnostics.RuntimeExt;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile)]
    [Verb("!u", HelpText =
        "Displays an IL and machine code listing for the method that contains the specified instruction pointer.")]
    class Disassemble : ICommand
    {
        [Value(0, Required = true, Hexadecimal = true,
            HelpText = "The instruction pointer in the method that needs to be disassembled.")]
        public ulong InstructionPointer { get; set; }

        private CommandExecutionContext _context;
        private IDebugControl _control;
        private Dictionary<string, string[]> _sourceFileCache;

        public void Execute(CommandExecutionContext context)
        {
            _context = context;

            ClrMethod method = context.Runtime.GetMethodByAddress(InstructionPointer);
            if (method == null)
            {
                context.WriteErrorLine("There is no managed method at the address {0:x16}.", InstructionPointer);
                return;
            }

            _sourceFileCache = new Dictionary<string, string[]>();
            using (var target = context.CreateTemporaryDbgEngTarget())
            {
                _control = (IDebugControl)target.DebuggerInterface;
                DisassembleMethod(method);
            }
        }

        private void DisassembleMethod(ClrMethod method)
        {
            var module = method.Type.Module;
            string fileName = module.FileName;

            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(fileName);
            TypeDefinition type = assembly.MainModule.GetType(method.Type.Name);

            MethodDefinition methodDef = type.Methods.Single(
                m => m.MetadataToken.ToUInt32() == method.MetadataToken);

            _context.WriteLine("{0}", method.GetFullSignature());

            if (method.ILOffsetMap == null)
                return;

            var mapByOffset = (from map in method.ILOffsetMap
                               where map.ILOffset >= 0 // prolog is -2, epilog -3
                               where map.StartAddress <= map.EndAddress
                               orderby map.ILOffset
                               select map).ToArray();
            if (mapByOffset.Length == 0)
            {
                // The method doesn't have an offset map. Just print the whole thing.
                PrintInstructions(methodDef.Body.Instructions);
            }

            // This is the prologue, looks like it's always there, but it could
            // also be the only thing that's in the method
            var prologue = method.ILOffsetMap[0];
            if (prologue.ILOffset == -2) // -2 is a magic number for prologue
                DisassembleNative(prologue);

            for (int i = 0; i < mapByOffset.Length; ++i)
            {
                var map = mapByOffset[i];
                IEnumerable<Instruction> instructions;
                if (i == mapByOffset.Length - 1)
                {
                    instructions = methodDef.Body.Instructions.Where(
                        instr => instr.Offset >= map.ILOffset);
                }
                else
                {
                    instructions = methodDef.Body.Instructions.Where(
                        instr => instr.Offset >= map.ILOffset &&
                            instr.Offset < mapByOffset[i + 1].ILOffset);
                }

                var sourceLocation = method.GetSourceLocation(map.ILOffset);
                if (sourceLocation != null)
                {
                    _context.WriteLine("{0} {1}-{2}:{3}-{4}", sourceLocation.FilePath,
                        sourceLocation.LineNumber, sourceLocation.LineNumberEnd,
                        sourceLocation.ColStart, sourceLocation.ColEnd);
                    for (int line = sourceLocation.LineNumber; line <= sourceLocation.LineNumberEnd; ++line)
                    {
                        var sourceLine = ReadSourceLine(sourceLocation.FilePath, line);

                        if (sourceLine != null)
                        {
                            _context.WriteLine(sourceLine);
                            _context.WriteLine(new string(' ', sourceLocation.ColStart - 1) + new string('^', sourceLocation.ColEnd - sourceLocation.ColStart));
                        }
                    }
                }
                PrintInstructions(instructions);
                DisassembleNative(map);
            }

            var epilogue = method.ILOffsetMap[method.ILOffsetMap.Length - 1];
            if (epilogue.ILOffset == -3) // -3 is a magic number for epilog
                DisassembleNative(epilogue);
        }

        private string ReadSourceLine(string file, int line)
        {
            string[] contents;
            if (!_sourceFileCache.TryGetValue(file, out contents))
            {
                contents = File.ReadAllLines(file);
                _sourceFileCache.Add(file, contents);
            }

            return line - 1 < contents.Length 
                ? contents[line - 1]
                : null; // "nop" can have no corresponding c# code
        }

        private void PrintInstructions(IEnumerable<Instruction> instructions)
        {
            foreach (var instr in instructions)
            {
                _context.WriteLine(instr.ToString());
            }
        }

        private void DisassembleNative(ILToNativeMap map)
        {
            ulong nextInstr;
            StringBuilder disasmBuffer = new StringBuilder(512);
            uint disasmSize;
            ulong disasmAddress = map.StartAddress;
            while (true)
            {
                int hr = _control.Disassemble(disasmAddress, 0,
                    disasmBuffer, disasmBuffer.Capacity, out disasmSize,
                    out nextInstr);
                if (hr != 0)
                    break;
                _context.Write(disasmBuffer.ToString());

                if (nextInstr >= map.EndAddress)
                    break;
                disasmAddress = nextInstr;
            }
        }
    }
}
