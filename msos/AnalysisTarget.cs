using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.RuntimeExt;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    class AnalysisTarget
    {
        const int ATTACH_TIMEOUT = 5000;
        const int WIN32_ACCESS_DENIED = 5;

        private string _dumpFile;
        private int _processId;
        private DataTarget _target;
        private CommandExecutionContext _context;
        private int _clrVersionIndex;

        public AnalysisTarget(string dumpFile, CommandExecutionContext context, int clrVersionIndex = 0)
        {
            _context = context;
            _clrVersionIndex = clrVersionIndex;

            _dumpFile = dumpFile;
            OpenDumpFile();
            
            SharedInit();
        }

        public AnalysisTarget(int pid, CommandExecutionContext context, int clrVersionIndex = 0)
        {
            _context = context;
            _clrVersionIndex = clrVersionIndex;

            _processId = pid;
            AttachToProcess();

            SharedInit();
        }

        private void SharedInit()
        {
            VerifyTargetArchitecture();
            VerifyCLRVersion();
            SetupSymPath();
            CreateRuntime();
            PerformAutomaticAnalysis();
        }

        private void PerformAutomaticAnalysis()
        {
            var threadToSwitchTo = _context.Runtime.ThreadWithActiveExceptionOrFirstThread();
            if (threadToSwitchTo.CurrentException != null)
            {
                _context.WriteInfoLine("The current thread has an exception; use !pe to view it.");
            }
            new SwitchThread() { ManagedThreadId = threadToSwitchTo.ManagedThreadId }.Execute(_context);

            if (_context.Runtime.OutOfMemoryExceptionOccurred)
            {
                _context.WriteInfoLine("There was an out-of-memory condition in this target:");
                var oomInfo = _context.Runtime.OutOfMemoryInformation;
                _context.WriteInfoLine("\tAn OOM occurred after GC {0} when trying to allocate {1}",
                    oomInfo.GCNumber, oomInfo.AllocationSize.ToMemoryUnits());
                _context.WriteInfoLine("\t" + oomInfo.Reason);
            }
        }

        private void Bail(string format, params object[] args)
        {
            string errorMessage = String.Format(format, args);
            _context.WriteErrorLine(errorMessage);
            throw new AnalysisFailedException(errorMessage);
        }

        private void OpenDumpFile()
        {
            if (!File.Exists(_dumpFile))
            {
                Bail("The specified dump file '{0}' does not exist.", _dumpFile);
            }

            _target = DataTarget.LoadCrashDump(_dumpFile, CrashDumpReader.ClrMD);
            _context.WriteInfoLine("Opened dump file '{0}', architecture {1}, {2} CLR versions detected.",
                _dumpFile, _target.Architecture, _target.ClrVersions.Count);
            _context.DumpFile = _dumpFile;
            _context.TargetType = _target.IsHeapAvailable ? TargetType.DumpFile : TargetType.DumpFileNoHeap;

            if (!_target.IsHeapAvailable)
            {
                _context.WriteWarningLine(
                    "This dump does not have heap information present. Most commands will not work. " + 
                    "Basic triage commands such as !pe, !clrstack, !threads are still available.");
            }
            if (_target.IsMinidump)
            {
                _context.WriteWarningLine(
                    "This dump is a minidump, which means it's possible that module contents were " +
                    "not included in the file. To get good information, make sure to put your modules " +
                    "(.exe/.dll files) on the symbol path, not just the symbols (.pdb files).");
            }
        }

        private void AttachToProcess()
        {
            VerifyTargetProcessArchitecture();

            _target = DataTarget.AttachToProcess(_processId, ATTACH_TIMEOUT, AttachFlag.Passive);
            _context.WriteInfoLine("Attached to process {0}, architecture {1}, {2} CLR versions detected.",
                _processId, _target.Architecture, _target.ClrVersions.Count);
            _context.ProcessId = _processId;
            _context.TargetType = TargetType.LiveProcess;
        }

        private void CreateRuntime()
        {
            ClrInfo clrInfo = _target.ClrVersions[_clrVersionIndex];
            string dacLocation;
            _context.Runtime = _target.CreateRuntimeAndGetDacLocation(clrInfo, out dacLocation);
            _context.WriteInfoLine("Using Data Access DLL at: " + dacLocation);
            _context.DacLocation = dacLocation;
            _context.ClrVersionIndex = _clrVersionIndex;
            _context.ClrVersion = clrInfo;
            _context.Heap = _context.Runtime.GetHeap();
        }

        private void SetupSymPath()
        {
            string symPath = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH");
            _context.WriteInfoLine("Symbol path: " + symPath);
            _context.SymbolLocator = _target.SymbolLocator;
            _context.SymbolLocator.SymbolPath = symPath;
            _context.SymbolPath = symPath;
        }

        private void VerifyCLRVersion()
        {
            if (_target.ClrVersions.Count == 0)
            {
                Bail("There is no CLR loaded into the process.");
            }
            for (int i = 0; i < _target.ClrVersions.Count; ++i)
            {
                var clrVersion = _target.ClrVersions[i];
                _context.WriteInfoLine("#{0} Flavor: {1}, Version: {2}", i, clrVersion.Flavor, clrVersion.Version);
            }
            if (_clrVersionIndex < 0 || _clrVersionIndex >= _target.ClrVersions.Count)
            {
                Bail("The ordinal number of the CLR to interact with is not valid. {0} specified, valid values are 0-{1}.",
                    _clrVersionIndex, _target.ClrVersions.Count - 1);
            }
            if (_target.ClrVersions.Count > 1)
            {
                _context.WriteInfoLine("The rest of this session will interact with CLR version #{0} ({1}). Change using --clrVersion.",
                    _clrVersionIndex, _target.ClrVersions[_clrVersionIndex].Version);
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

        private void VerifyTargetProcessArchitecture()
        {
            Process process = null;
            try
            {
                process = Process.GetProcessById(_processId);
            }
            catch (ArgumentException ex)
            {
                Bail("Error opening process {0}: {1}", _processId, ex.Message);
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
    }
}
