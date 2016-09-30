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
            _context.WriteErrorLine(format, args);
            Bail();
        }

        private void Bail()
        {
            Exit(ERROR_EXIT_CODE);
        }

        private void Exit(int exitCode)
        {
            _context.Dispose();
            Environment.Exit(exitCode);
        }

        private CommandLineOptions _options;
        private AnalysisTarget _target;
        private CommandExecutionContext _context = new CommandExecutionContext();
        private CmdLineParser _parser;

        private void Run()
        {
            // The NO_CONSOLE environment variable requests that console modifications such as the following one
            // are not performed. This is necessary in no-console environments such as Azure App Service.
            if (Environment.GetEnvironmentVariable("NO_CONSOLE") == null)
            {
                const int ConsoleBufferSize = 4096;
                Console.SetIn(new StreamReader(
                    Console.OpenStandardInput(bufferSize: ConsoleBufferSize), Console.InputEncoding, false, ConsoleBufferSize)
                    );
            }

            Console.BackgroundColor = ConsoleColor.Black;
            _context.Printer = new ConsolePrinter();
            _parser = new CmdLineParser(new PrinterTextWriter(_context.Printer));

            ParseCommandLineArguments();
            _context.DisplayDiagnosticInformation = _options.DisplayDiagnosticInformation;

            if (!String.IsNullOrEmpty(_options.DumpFile))
            {
                _target = new AnalysisTarget(_options.DumpFile, _context, _options.ClrVersion);
                Console.Title = "msos - " + _options.DumpFile;
            }
            else if (!String.IsNullOrEmpty(_options.SummaryDumpFile))
            {
                DisplayShortSummary();
                return; // Do not proceed to the main loop
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

        private static string[] GetFilesFromPattern(string dumpPattern)
        {
            string directory = Path.GetDirectoryName(dumpPattern);
            string pattern = Path.GetFileName(dumpPattern);
            return Directory.GetFiles(directory, pattern, SearchOption.AllDirectories);
        }

        private void DisplayShortSummary()
        {
            // We are using the DbgEng dump reader here, because the CLRMD dump reader doesn't work
            // well with mismatched architectures. For the sake of just getting basic information out
            // there, the DbgEng reader is better.
            string[] dumpFiles = GetFilesFromPattern(_options.SummaryDumpFile);
            foreach (var dumpFile in dumpFiles)
            {
                using (var target = DataTarget.LoadCrashDump(dumpFile, CrashDumpReader.DbgEng))
                {
                    _context.WriteInfoLine("Summary for dump file {0}", dumpFile);
                    _context.WriteLine("  Is minidump? {0}", target.IsMinidump);
                    _context.WriteLine("  Target architecture: {0}", target.Architecture);
                    _context.WriteLine("  {0} CLR versions in target", target.ClrVersions.Count);
                    foreach (var clrInfo in target.ClrVersions)
                        _context.WriteLine("    {0}", clrInfo.Version);
                    _context.WriteLine();
                }
            }
        }

        private void RunTriage()
        {
            string[] dumpFiles = GetFilesFromPattern(_options.TriagePattern);
            _context.WriteInfoLine("Triage: enumerated {0} dump files in directory '{1}'", dumpFiles.Length, Path.GetDirectoryName(_options.TriagePattern));
            Dictionary<string, TriageInformation> triages = new Dictionary<string, TriageInformation>();
            for (int i = 0; i < dumpFiles.Length; ++i)
            {
                string dumpFile = dumpFiles[i];
                string analysisProgressMessage = String.Format("Analyzing dump file '{0}' ({1}/{2})", dumpFile, i + 1, dumpFiles.Length);
                _context.WriteInfoLine(analysisProgressMessage);
                Console.Title = analysisProgressMessage;

                _target = new AnalysisTarget(dumpFile, _context);
                TriageInformation triageInformation = new Triage().GetTriageInformation(_context);
                triages.Add(dumpFile, triageInformation);
            }

            _context.WriteLine("{0,-30} {1,-30} {2,-50} {3,-30}", "DUMP", "EVENT", "METHOD", "MEMORY (CMT/RSV/MGD)");
            foreach (var kvp in triages)
            {
                string dumpFile = kvp.Key;
                TriageInformation triageInformation = kvp.Value;
                _context.WriteLine("{0,-30} {1,-30} {2,-50} {3,-30}",
                    dumpFile.TrimStartToLength(30),
                    triageInformation.GetEventDisplayString().TrimStartToLength(30),
                    (triageInformation.FaultingMethod ?? "N/A").TrimStartToLength(50),
                    String.Format("{0}/{1}/{2}",
                        triageInformation.CommittedMemoryBytes.ToMemoryUnits(),
                        triageInformation.ReservedMemoryBytes.ToMemoryUnits(),
                        triageInformation.GCHeapMemoryBytes.ToMemoryUnits()
                        )
                    );
            }

            var groupedByModule = from triage in triages.Values
                                  group triage by triage.FaultingModule into g
                                  let count = g.Count()
                                  select new { Module = g.Key, Count = count };
            _context.WriteLine();
            _context.WriteLine("{0,-30} {1,-12}", "MODULE", "COUNT");
            foreach (var moduleCount in groupedByModule)
            {
                _context.WriteLine("{0,-30} {1,-12}", moduleCount.Module, moduleCount.Count);
            }

            var groupedByEvent = from triage in triages.Values
                                 group triage by triage.GetEventDisplayString() into g
                                 let count = g.Count()
                                 select new { Event = g.Key, Count = count };
            _context.WriteLine();
            _context.WriteLine("{0,-50} {1,-12}", "EVENT", "COUNT");
            foreach (var eventCount in groupedByEvent)
            {
                _context.WriteLine("{0,-50} {1,-12}", eventCount.Event, eventCount.Count);
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

                _context.ExecuteOneCommand(command);
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
                    _context.WriteInfoLine("#> {0}", command);
                    _context.ExecuteOneCommand(command);
                }
            }
            else if (!String.IsNullOrEmpty(_options.InitialCommand))
            {
                _context.WriteInfoLine("#> {0}", _options.InitialCommand);
                _context.ExecuteCommand(_options.InitialCommand);
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
                _context.WriteErrorLine("There is more than one process matching the name '{0}', use --pid to disambiguate.", processName);
                _context.WriteInfoLine("Matching process ids: {0}", String.Join(", ", processes.Select(p => p.Id).ToArray()));
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
                _context.WriteErrorLine("An unexpected error occurred.");
                _context.WriteErrorLine("{0}: {1}", ex.GetType().Name, ex.Message);
                if (_options.DisplayDiagnosticInformation)
                {
                    _context.WriteErrorLine("\n" + ex.StackTrace);
                }
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
