using CommandLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    class Program
    {
        const int SUCCESS_EXIT_CODE = 0;
        const int ERROR_EXIT_CODE = 1;

        static Type[] GetAllCommands()
        {
            return (from type in Assembly.GetExecutingAssembly().GetTypes()
                    where typeof(ICommand).IsAssignableFrom(type)
                    select type
                    ).ToArray();
        }

        static int Main(string[] args)
        {
            Console.BackgroundColor = ConsoleColor.Black;

            var commandLineOptions = Parser.Default.ParseArguments<CommandLineOptions>(args);
            if (commandLineOptions.Errors.Any())
            {
                return ERROR_EXIT_CODE;
            }

            var target = DataTarget.LoadCrashDump(commandLineOptions.Value.DumpFile, CrashDumpReader.ClrMD);
            ConsolePrinter.WriteInfo("Opened dump file '{0}', architecture {1}, {2} CLR versions detected.",
                commandLineOptions.Value.DumpFile, target.Architecture, target.ClrVersions.Count);
            Console.Title = "msos - " + commandLineOptions.Value.DumpFile;

            if (target.Architecture == Architecture.Amd64 && !Environment.Is64BitProcess)
            {
                ConsolePrinter.WriteError("You must use the 64-bit version of this application to analyze this dump.");
                return ERROR_EXIT_CODE;
            }
            if (target.Architecture != Architecture.Amd64 && Environment.Is64BitProcess)
            {
                ConsolePrinter.WriteError("You must use the 32-bit version of this application to analyze this dump.");
                return ERROR_EXIT_CODE;
            }

            foreach (var clrVersion in target.ClrVersions)
            {
                ConsolePrinter.WriteInfo("Flavor: {0}, version: {1}", clrVersion.Flavor, clrVersion.Version);
            }
            if (target.ClrVersions.Count > 1)
            {
                ConsolePrinter.WriteInfo("The rest of this session will interact with the first CLR version.");
            }

            string symPath = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH");
            ConsolePrinter.WriteInfo("Symbol path: " + symPath);
            target.AppendSymbolPath(symPath);

            string dacLocation = target.ClrVersions[0].TryDownloadDac();
            ConsolePrinter.WriteInfo("Using Data Access DLL at: " + dacLocation);
            var runtime = target.CreateRuntime(dacLocation);

            // TODO If there is a current exception on the current thread, print it
            // TODO Set the context to the first managed thread, or the one with the exception

            var context = new CommandExecutionContext(runtime);
            target.DefaultSymbolNotification = new SymbolNotification(context);
            var commandParser = new Parser(ps =>
            {
                ps.CaseSensitive = false;
                ps.HelpWriter = Console.Out;
            });
            while (!context.ShouldQuit)
            {
                Console.Write("{0}> ", context.CurrentManagedThreadId);

                string input = Console.ReadLine();
                string[] parts = input.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;

                var parseResult = commandParser.ParseArguments(parts, GetAllCommands());
                if (parseResult.Errors.Any())
                    continue;

                var stopwatch = Stopwatch.StartNew();
                (parseResult.Value as ICommand).Execute(context);
                ConsolePrinter.WriteInfo("Elapsed: {0}ms", stopwatch.ElapsedMilliseconds);
            }

            return SUCCESS_EXIT_CODE;
        }
    }
}
