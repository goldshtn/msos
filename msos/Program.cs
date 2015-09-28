using CmdLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    class Program : IDisposable
    {
        const int SUCCESS_EXIT_CODE = 0;
        const int ERROR_EXIT_CODE = 1;

        private void Bail(string format, params object[] args)
        {
            _context.WriteError(format, args);
            Bail();
        }

        private void Bail()
        {
            Exit(ERROR_EXIT_CODE);
        }

        private void Exit(int exitCode)
        {
            _context.Dispose();
            Environment.Exit(ERROR_EXIT_CODE);
        }

        private CommandLineOptions _options;
        private AnalysisTarget _target;
        private CommandExecutionContext _context = new CommandExecutionContext();
        private CmdLineParser _parser;

        private void Run()
        {
            const int ConsoleBufferSize = 4096;
            Console.SetIn(new StreamReader(
                Console.OpenStandardInput(bufferSize: ConsoleBufferSize), Console.InputEncoding, false, ConsoleBufferSize)
                );

            Console.BackgroundColor = ConsoleColor.Black;
            _context.Printer = new ConsolePrinter();
            _parser = new CmdLineParser(new PrinterTextWriter(_context.Printer));

            ParseCommandLineArguments();

            if (!String.IsNullOrEmpty(_options.DumpFile))
            {
                _target = new AnalysisTarget(_options.DumpFile, _context, _options.ClrVersion);
                Console.Title = "msos - " + _options.DumpFile;
            }
            else if (!String.IsNullOrEmpty(_options.ProcessName))
            {
                AttachToProcessByName();
            }
            else if (_options.ProcessId != 0)
            {
                _target = new AnalysisTarget(_options.ProcessId, _context, _options.ClrVersion);
                Console.Title = "msos - attached to pid " + _options.ProcessId;
            }
            else if (!String.IsNullOrEmpty(_options.TriagePattern))
            {
                RunTriage();
                return; // Do not proceed to the main loop
            }
            else
            {
                PrintUsage();
                Bail("One of the -z, --pid, --pn, or --triage options must be specified.");
            }

            RunMainLoop();
        }

        private void RunTriage()
        {
            string directory = Path.GetDirectoryName(_options.TriagePattern);
            string pattern = Path.GetFileName(_options.TriagePattern);
            string[] dumpFiles = Directory.GetFiles(directory, pattern /* TODO: recursively enumerate? */);
            _context.WriteInfo("Triage: enumerated {0} dump files in directory '{1}'", dumpFiles.Length, directory);
            Dictionary<string, TriageInformation> triages = new Dictionary<string, TriageInformation>();
            for (int i = 0; i < dumpFiles.Length; ++i)
            {
                string dumpFile = dumpFiles[i];
                string analysisProgressMessage = String.Format("Analyzing dump file '{0}' ({1}/{2})", dumpFile, i + 1, dumpFiles.Length);
                _context.WriteInfo(analysisProgressMessage);
                Console.Title = analysisProgressMessage;

                _target = new AnalysisTarget(dumpFile, _context);
                TriageInformation triageInformation = new Triage().GetTriageInformation(_context);
                triages.Add(dumpFile, triageInformation);
            }

            _context.WriteLine("{0,-30} {1,-30} {2,-20} {3,-30} {4,-30}", "Dump", "Event", "Module", "Method", "Mem (Cmt/Rsv/Mgd)");
            foreach (var kvp in triages)
            {
                string dumpFile = kvp.Key;
                TriageInformation triageInformation = kvp.Value;
                _context.WriteLine("{0,-30} {1,-30} {2,-20} {3,-30} {4,-30}",
                    dumpFile.TrimStartToLength(30),
                    (triageInformation.ManagedExceptionType ?? triageInformation.EventDescription.Replace(" (first/second chance not available)", "")).TrimStartToLength(30),
                    (triageInformation.FaultingModule ?? "N/A").TrimStartToLength(20),
                    (triageInformation.FaultingMethod ?? "N/A").TrimStartToLength(30),
                    String.Format("{0}/{1}/{2}",
                        triageInformation.CommittedMemoryBytes.ToMemoryUnits(),
                        triageInformation.ReservedMemoryBytes.ToMemoryUnits(),
                        triageInformation.GCHeapMemoryBytes.ToMemoryUnits()
                        )
                    );
            }
        }

        private void PrintUsage()
        {
            _context.WriteLine(_parser.Banner());
            _context.WriteLine(_parser.Usage<CommandLineOptions>());
        }

        private void RunMainLoop()
        {
            ExecuteInitialCommand();

            while (!_context.ShouldQuit)
            {
                Console.Write(_context.Prompt);

                string command = "";
                while (true)
                {
                    string input = Console.ReadLine();
                    if (input.EndsWith(" _"))
                    {
                        Console.Write(">    ");
                        command += input.Substring(0, input.Length - 1);
                    }
                    else
                    {
                        command += input;
                        break;
                    }
                }

                _context.ExecuteOneCommand(command, _options.DisplayDiagnosticInformation);
            }
        }

        private void ExecuteInitialCommand()
        {
            if (!String.IsNullOrEmpty(_options.InputFileName))
            {
                List<string> commands = new List<string>();
                try
                {
                    string command = "";
                    foreach (string line in File.ReadLines(_options.InputFileName))
                    {
                        if (line.EndsWith(" _"))
                        {
                            command += line.Substring(0, line.Length - 1);
                        }
                        else
                        {
                            commands.Add(command + line);
                            command = "";
                        }
                    }
                }
                catch (IOException ex)
                {
                    Bail("Error reading from initial command file: {0}", ex.Message);
                }

                foreach (var command in commands)
                {
                    _context.WriteInfo("#> {0}", command);
                    _context.ExecuteOneCommand(command, _options.DisplayDiagnosticInformation);
                }
            }
            else if (!String.IsNullOrEmpty(_options.InitialCommand))
            {
                _context.WriteInfo("#> {0}", _options.InitialCommand);
                _context.ExecuteCommand(_options.InitialCommand, _options.DisplayDiagnosticInformation);
            }
        }

        private void AttachToProcessByName()
        {
            string processName = _options.ProcessName;
            Process[] processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
            {
                Bail("There are no processes matching the name '{0}'.", processName);
            }
            if (processes.Length > 1)
            {
                _context.WriteError("There is more than one process matching the name '{0}', use --pid to disambiguate.", processName);
                _context.WriteInfo("Matching process ids: {0}", String.Join(", ", processes.Select(p => p.Id).ToArray()));
                Bail();
            }
            _target = new AnalysisTarget(processes[0].Id, _context, _options.ClrVersion);
        }

        private void ParseCommandLineArguments()
        {
            string commandLine = CommandLineNoExecutableName();
            var parseResult = _parser.Parse<CommandLineOptions>(commandLine);
            if (!parseResult.Success)
            {
                Bail(parseResult.Error);
            }
            _options = parseResult.Value;

            if (!String.IsNullOrEmpty(_options.OutputFileName))
            {
                try
                {
                    var filePrinter = new FilePrinter(_options.OutputFileName);
                    _context.Printer = filePrinter;
                }
                catch (IOException ex)
                {
                    Bail("Error creating output file: {0}", ex.Message);
                }
            }
        }

        private static string CommandLineNoExecutableName()
        {
            string commandLine = Environment.CommandLine;
            if (commandLine[0] == '"')
            {
                commandLine = commandLine.Substring(commandLine.IndexOf('"', 1) + 1);
            }
            else
            {
                int firstSpace = commandLine.IndexOf(' ');
                if (firstSpace == -1)
                {
                    commandLine = "";
                }
                else
                {
                    commandLine = commandLine.Substring(firstSpace + 1);
                }
            }
            return commandLine;
        }

        public void Dispose()
        {
            Exit(SUCCESS_EXIT_CODE);
        }

        private void RunWrapper()
        {
            try
            {
                Run();
            }
            catch (AnalysisFailedException)
            {
                // The exception message is already printed by Bail(),
                // so there is no need to do anything special here but exit.
            }
            catch (Exception ex)
            {
                _context.WriteError("An unexpected error occurred.");
                _context.WriteError("{0}: {1}", ex.GetType().Name, ex.Message);
            }
        }

        static void Main()
        {
            using (var program = new Program())
            {
                program.RunWrapper();
            }
        }
    }
}
