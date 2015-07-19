using CommandLine;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!lm", HelpText = "Displays the loaded modules.")]
    class LM : ICommand
    {
        [Option("native", HelpText = "Whether to display native (unmanaged) modules.")]
        public bool DisplayNativeModules { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            context.WriteLine("{0,-20:x16} {1,-10:x16} {2,-10} {3}", "start", "size", "symloaded", "filename");
            foreach (var module in context.Runtime.EnumerateModules())
            {
                context.WriteLine("{0,-20:x16} {1,-10:x8} {2,-10} {3}",
                    module.ImageBase, module.Size, module.IsPdbLoaded, module.FileName);
            }

            if (!DisplayNativeModules)
                return;

            using (var target = context.CreateDbgEngTarget())
            {
                IDebugSymbols3 debugSymbols = (IDebugSymbols3)target.DebuggerInterface;
                uint loaded, unloaded;
                if (0 != debugSymbols.GetNumberModules(out loaded, out unloaded))
                    return;

                for (uint modIdx = 0; modIdx < loaded; ++modIdx)
                {
                    DEBUG_MODULE_PARAMETERS[] modInfo = new DEBUG_MODULE_PARAMETERS[1];
                    if (0 != debugSymbols.GetModuleParameters(1, null, modIdx, modInfo))
                        continue;

                    StringBuilder modName = new StringBuilder(2048);
                    uint dummy;
                    if (0 != debugSymbols.GetModuleNameString(DEBUG_MODNAME.IMAGE,
                        modIdx, 0, modName, (uint)modName.Capacity, out dummy))
                    {
                        modName.Append("<unknown>");
                    }

                    context.WriteLine("{0,-20:x16} {1,-10:x8} {2,-10} {3}",
                        modInfo[0].Base, modInfo[0].Size,
                        modInfo[0].SymbolType == DEBUG_SYMTYPE.PDB || modInfo[0].SymbolType == DEBUG_SYMTYPE.DIA,
                        modName.ToString());
                }
            }
        }
    }
}
