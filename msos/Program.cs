using CommandLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    class Program : IDisposable
    {
        const int SUCCESS_EXIT_CODE = 0;
        const int ERROR_EXIT_CODE = 1;
        const int ATTACH_TIMEOUT = 5000;
        const int WIN32_ACCESS_DENIED = 5;

        private static Type[] GetAllCommandTypes()
        {
            return (from type in Assembly.GetExecutingAssembly().GetTypes()
                    where typeof(ICommand).IsAssignableFrom(type)
                    select type
                    ).ToArray();
        }

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
        private DataTarget _target;
        private ClrRuntime _runtime;
        private CommandExecutionContext _context = new CommandExecutionContext();
        private Parser _commandParser;
        private Type[] _allCommandTypes;

        private void Run(string[] args)
        {
            ParseCommandLineArguments(args);

            if (!String.IsNullOrEmpty(_options.DumpFile))
            {
                OpenDumpFile();
            }
            else if (!String.IsNullOrEmpty(_options.ProcessName))
            {
                AttachToProcessByName();
            }
            else if (_options.ProcessId != 0)
            {
                AttachToProcessById();
            }
            else
            {
                Bail("One of the -z, --pid, or --pn options must be specified. Try --help.");
            }

            VerifyTargetArchitecture();

            VerifyCLRVersion();

            SetupSymPath();

            CreateRuntime();

            RunMainLoop();
        }

        private void RunMainLoop()
        {
            _context.Runtime = _runtime;
            _target.DefaultSymbolNotification = new SymbolNotification(_context);

            var threadToSwitchTo = _runtime.ThreadWithActiveExceptionOrFirstThread();
            new SwitchThread() { ManagedThreadId = threadToSwitchTo.ManagedThreadId }.Execute(_context);

            _commandParser = new Parser(ps =>
            {
                ps.CaseSensitive = false;
                ps.HelpWriter = Console.Out;
            });
            _allCommandTypes = GetAllCommandTypes();

            ExecuteInitialCommand();

            while (!_context.ShouldQuit)
            {
                Console.Write("{0}> ", _context.CurrentManagedThreadId);

                string input = Console.ReadLine();
                ExecuteOneCommand(input);
            }
        }

        private void ExecuteInitialCommand()
        {
            string[] commands = null;
            if (!String.IsNullOrEmpty(_options.InputFileName))
            {
                try
                {
                    commands = File.ReadAllLines(_options.InputFileName);
                }
                catch (IOException ex)
                {
                    Bail("Error reading from initial command file: {0}", ex.Message);
                }
            }
            else if (!String.IsNullOrEmpty(_options.InitialCommand))
            {
                commands = _options.InitialCommand.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            }

            if (commands == null)
                return;

            foreach (var command in commands)
            {
                _context.WriteInfo("> {0}", command);
                ExecuteOneCommand(command);
            }
        }
        
        private void ExecuteOneCommand(string command)
        {
            string[] parts = command.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return;

            if (parts[0] == "#")
                return; // Lines starting with # are comments

            var parseResult = _commandParser.ParseArguments(parts, _allCommandTypes);
            if (parseResult.Errors.Any())
                return;

            using (new TimeAndMemory(_options.DisplayDiagnosticInformation, _context.Printer))
            {
                ((ICommand)parseResult.Value).Execute(_context);
            }
        }

        private void CreateRuntime()
        {
            string dacLocation = _target.ClrVersions[0].TryDownloadDac();
            _context.WriteInfo("Using Data Access DLL at: " + dacLocation);
            _runtime = _target.CreateRuntime(dacLocation);
            _context.DacLocation = dacLocation;
        }

        private void SetupSymPath()
        {
            string symPath = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH");
            _context.WriteInfo("Symbol path: " + symPath);
            _target.AppendSymbolPath(symPath);
        }

        private void VerifyCLRVersion()
        {
            if (_target.ClrVersions.Count == 0)
            {
                Bail("There is no CLR loaded into the process.");
            }
            foreach (var clrVersion in _target.ClrVersions)
            {
                _context.WriteInfo("Flavor: {0}, version: {1}", clrVersion.Flavor, clrVersion.Version);
            }
            if (_target.ClrVersions.Count > 1)
            {
                _context.WriteInfo("The rest of this session will interact with the first CLR version.");
            }
        }

        private void VerifyTargetArchitecture()
        {
            if (_target.Architecture == Architecture.Amd64 && !Environment.Is64BitProcess)
            {
                Bail("You must use the 64-bit version of this application.");
            }
            if (_target.Architecture != Architecture.Amd64 && Environment.Is64BitProcess)
            {
                Bail("You must use the 32-bit version of this application.");
            }
        }

        private void VerifyTargetArchitecture(int pid)
        {
            Process process = null;
            try
            {
                process = Process.GetProcessById(pid);
            }
            catch (ArgumentException ex)
            {
                Bail("Error opening process {0}: {1}", pid, ex.Message);
            }

            if (Environment.Is64BitOperatingSystem)
            {
                IntPtr handle = IntPtr.Zero;
                try
                {
                    handle = process.Handle;
                }
                catch (Win32Exception ex)
                {
                    if (ex.NativeErrorCode == WIN32_ACCESS_DENIED)
                    {
                        Bail("You do not have sufficient privileges to attach to the target process. Try running as administrator.");
                    }
                    Bail("An error occurred while retrieving the process handle. Error code: {0}", ex.NativeErrorCode);
                }

                bool isTarget32Bit;
                if (!NativeMethods.IsWow64Process(handle, out isTarget32Bit))
                {
                    Bail("Unable to determine whether target is a 64-bit process.");
                }
                if (!isTarget32Bit && !Environment.Is64BitProcess)
                {
                    Bail("You must use the 64-bit version of this application.");
                }
                if (isTarget32Bit && Environment.Is64BitProcess)
                {
                    Bail("You must use the 32-bit version of this application.");
                }
            }
        }

        private void AttachToProcessById(int pid = 0)
        {
            if (pid == 0)
            {
                pid = _options.ProcessId;
            }

            VerifyTargetArchitecture(pid);

            _target = DataTarget.AttachToProcess(pid, ATTACH_TIMEOUT, AttachFlag.Passive);
            _context.WriteInfo("Attached to process {0}, architecture {1}, {2} CLR versions detected.",
                pid, _target.Architecture, _target.ClrVersions.Count);
            _context.ProcessId = pid;
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
            AttachToProcessById(processes[0].Id);
        }

        private void OpenDumpFile()
        {
            _target = DataTarget.LoadCrashDump(_options.DumpFile, CrashDumpReader.ClrMD);
            _context.WriteInfo("Opened dump file '{0}', architecture {1}, {2} CLR versions detected.",
                _options.DumpFile, _target.Architecture, _target.ClrVersions.Count);
            Console.Title = "msos - " + _options.DumpFile;
            _context.DumpFile = _options.DumpFile;
        }

        private void ParseCommandLineArguments(string[] args)
        {
            // Start with the default console printer before parsing any arguments.
            Console.BackgroundColor = ConsoleColor.Black;
            _context.Printer = new ConsolePrinter();

            var options = Parser.Default.ParseArguments<CommandLineOptions>(args);
            if (options.Errors.Any())
            {
                Bail();
            }
            _options = options.Value;

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

        public void Dispose()
        {
            Exit(SUCCESS_EXIT_CODE);
        }

        static void Main(string[] args)
        {
            using (var program = new Program())
            {
                program.Run(args);
            }
        }
    }
}
