using CommandLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    class Program
    {
        const int ERROR_EXIT_CODE = 1;
        const int ATTACH_TIMEOUT = 5000;
        const int WIN32_ACCESS_DENIED = 5;

        private static Type[] GetAllCommands()
        {
            return (from type in Assembly.GetExecutingAssembly().GetTypes()
                    where typeof(ICommand).IsAssignableFrom(type)
                    select type
                    ).ToArray();
        }

        private static void Bail(string format, params object[] args)
        {
            ConsolePrinter.WriteError(format, args);
            Bail();
        }

        private static void Bail()
        {
            Environment.Exit(ERROR_EXIT_CODE);
        }

        private CommandLineOptions _options;
        private DataTarget _target;
        private ClrRuntime _runtime;

        private void Run(string[] args)
        {
            Console.BackgroundColor = ConsoleColor.Black;

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
            var context = new CommandExecutionContext(_runtime);
            _target.DefaultSymbolNotification = new SymbolNotification(context);

            var threadToSwitchTo = _runtime.ThreadWithActiveExceptionOrFirstThread();
            new SwitchThread() { ManagedThreadId = threadToSwitchTo.ManagedThreadId }.Execute(context);

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
                ((ICommand)parseResult.Value).Execute(context);
                ConsolePrinter.WriteInfo("Elapsed: {0}ms", stopwatch.ElapsedMilliseconds);
            }
        }

        private void CreateRuntime()
        {
            string dacLocation = _target.ClrVersions[0].TryDownloadDac();
            ConsolePrinter.WriteInfo("Using Data Access DLL at: " + dacLocation);
            _runtime = _target.CreateRuntime(dacLocation);
        }

        private void SetupSymPath()
        {
            string symPath = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH");
            ConsolePrinter.WriteInfo("Symbol path: " + symPath);
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
                ConsolePrinter.WriteInfo("Flavor: {0}, version: {1}", clrVersion.Flavor, clrVersion.Version);
            }
            if (_target.ClrVersions.Count > 1)
            {
                ConsolePrinter.WriteInfo("The rest of this session will interact with the first CLR version.");
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

        private static void VerifyTargetArchitecture(int pid)
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
            ConsolePrinter.WriteInfo("Attached to process {0}, architecture {1}, {2} CLR versions detected.",
                pid, _target.Architecture, _target.ClrVersions.Count);
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
                ConsolePrinter.WriteError("There is more than one process matching the name '{0}', use --pid to disambiguate.", processName);
                ConsolePrinter.WriteInfo("Matching process ids: {0}", String.Join(", ", processes.Select(p => p.Id).ToArray()));
                Bail();
            }
            AttachToProcessById(processes[0].Id);
        }

        private void OpenDumpFile()
        {
            _target = DataTarget.LoadCrashDump(_options.DumpFile, CrashDumpReader.ClrMD);
            ConsolePrinter.WriteInfo("Opened dump file '{0}', architecture {1}, {2} CLR versions detected.",
                _options.DumpFile, _target.Architecture, _target.ClrVersions.Count);
            Console.Title = "msos - " + _options.DumpFile;
        }

        private void ParseCommandLineArguments(string[] args)
        {
            var options = Parser.Default.ParseArguments<CommandLineOptions>(args);
            if (options.Errors.Any())
            {
                Bail();
            }
            _options = options.Value;
        }

        static void Main(string[] args)
        {
            new Program().Run(args);
        }
    }
}
