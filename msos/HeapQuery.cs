using CmdLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    enum HeapQueryOutputFormat
    {
        Tabular,
        Json
    }

    [SupportedTargets(TargetType.DumpFile, TargetType.LiveProcess)]
    [Verb("!hq", HelpText = "Runs a query over heap objects and prints the results. Useful helpers include AllObjects(), ObjectsOfType(\"TypeName\"), AllClasses(), and Class(\"TypeName\"). Special properties on objects include __Type and __Size; special properties on classes include __Fields and __StaticFields.")]
    class HeapQuery : ICommand
    {
        [Value(0, Required = true, HelpText = "The query output format (tabular or JSON).")]
        public HeapQueryOutputFormat OutputFormat { get; set; }

        [RestOfInput(Required = true, HelpText = "The query to execute.")]
        public string Query { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            AppDomainSetup setupInfo = new AppDomainSetup();
            setupInfo.ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;
            setupInfo.PrivateBinPath = "bin";
            AppDomain appDomain = AppDomain.CreateDomain("RunQueryAppDomain", null, setupInfo);

            string compilationOutputDirectory = null;
            object[] arguments;
            if (context.ProcessId != 0)
            {
                arguments = new object[] { context.ProcessId, context.ClrVersionIndex, context.DacLocation, context.Printer };
            }
            else
            {
                arguments = new object[] { context.DumpFile, context.ClrVersionIndex, context.DacLocation, context.Printer };
            }
            using (RunInSeparateAppDomain runner = (RunInSeparateAppDomain)appDomain.CreateInstanceAndUnwrap(
                typeof(RunInSeparateAppDomain).Assembly.FullName,
                typeof(RunInSeparateAppDomain).FullName,
                false, System.Reflection.BindingFlags.CreateInstance, null,
                arguments, null, null
                )
            )
            {
                try
                {
                    compilationOutputDirectory = runner.RunQuery(
                        OutputFormat.ToString(),
                        Query,
                        context.Defines);
                }
                catch (Exception ex)
                {
                    // Catching everything here because the input is user-controlled, so we can have 
                    // compilation errors, dynamic binder errors, and a variety of other things I haven't
                    // even thought of yet.
                    context.WriteError(ex.Message);
                }
            }

            AppDomain.Unload(appDomain);
            if (compilationOutputDirectory != null)
            {
                Directory.Delete(compilationOutputDirectory, recursive: true);
            }
        }
    }
}
