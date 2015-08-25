using CmdLine;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [SupportedTargets(TargetType.DumpFile, TargetType.DumpFileNoHeap, TargetType.LiveProcess)]
    [Verb("!lm", HelpText = "Displays the loaded modules.")]
    class LM : ICommand
    {
        [Option("symstate", HelpText = "Displays symbol load information for the specified module.")]
        public string SpecificModule { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            if (!String.IsNullOrEmpty(SpecificModule))
            {
                var module = context.Runtime.EnumerateModules().FirstOrDefault(
                    m => String.Equals(Path.GetFileName(m.Name), SpecificModule, StringComparison.InvariantCultureIgnoreCase));
                if (module != null)
                {
                    var moduleInfo = context.Runtime.DataTarget.EnumerateModules().Single(
                        m => String.Equals(m.FileName, module.FileName, StringComparison.InvariantCultureIgnoreCase));
                    context.WriteLine("Module:     {0}", module.Name);
                    context.WriteLine("PDB loaded: {0}", module.IsPdbLoaded);
                    context.WriteLine("PDB name:   {0}", moduleInfo.Pdb.FileName);
                    context.WriteLine("Debug mode: {0}", module.DebuggingMode);
                }
                else
                {
                    // Couldn't find managed module, try to find native:
                    using (var target = context.CreateTemporaryDbgEngTarget())
                    {
                        var moduleInfo = context.Runtime.DataTarget.EnumerateModules().FirstOrDefault(
                            m => String.Equals(Path.GetFileName(m.FileName), SpecificModule, StringComparison.InvariantCultureIgnoreCase));
                        if (moduleInfo == null)
                            return;

                        IDebugSymbols3 debugSymbols = (IDebugSymbols3)target.DebuggerInterface;

                        uint loaded, unloaded;
                        if (0 != debugSymbols.GetNumberModules(out loaded, out unloaded))
                            return;

                        for (uint moduleIdx = 0; moduleIdx < loaded; ++moduleIdx)
                        {
                            StringBuilder name = new StringBuilder(2048);
                            uint nameSize;
                            if (0 != debugSymbols.GetModuleNameString(DEBUG_MODNAME.IMAGE, moduleIdx, 0, name, (uint)name.Capacity, out nameSize))
                                continue;

                            if (!String.Equals(name.ToString(), moduleInfo.FileName, StringComparison.InvariantCultureIgnoreCase))
                                continue;

                            DEBUG_MODULE_PARAMETERS[] modInfo = new DEBUG_MODULE_PARAMETERS[1];
                            if (0 != debugSymbols.GetModuleParameters(1, null, moduleIdx, modInfo))
                                return;

                            name = new StringBuilder(2048);
                            debugSymbols.GetModuleNameString(DEBUG_MODNAME.SYMBOL_FILE, moduleIdx, 0, name, (uint)name.Capacity, out nameSize);

                            context.WriteLine("Module:     {0}", moduleInfo.FileName);
                            context.WriteLine("PDB loaded: {0}", modInfo[0].SymbolType == DEBUG_SYMTYPE.DIA || modInfo[0].SymbolType == DEBUG_SYMTYPE.PDB);
                            context.WriteLine("PDB name:   {0}", name.ToString());
                        }
                    }
                }
                return;
            }

            context.WriteLine("{0,-20:x16} {1,-10} {2,-20} {3}", "start", "size", "version", "filename");
            foreach (var module in context.Runtime.DataTarget.EnumerateModules())
            {
                context.WriteLine("{0,-20:x16} {1,-10:x} {2,-20} {3}",
                    module.ImageBase, module.FileSize, module.Version, module.FileName);
            }
        }
    }
}
