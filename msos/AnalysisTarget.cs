using Microsoft.Diagnostics.Runtime;
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
        }

        private void Bail(string format, params object[] args)
        {
            string errorMessage = String.Format(format, args);
            _context.WriteError(errorMessage);
            throw new AnalysisFailedException(errorMessage);
        }

        private void OpenDumpFile()
        {
            if (!File.Exists(_dumpFile))
            {
                Bail("The specified dump file '{0}' does not exist.", _dumpFile);
            }

            _target = DataTarget.LoadCrashDump(_dumpFile, CrashDumpReader.ClrMD);
            _context.WriteInfo("Opened dump file '{0}', architecture {1}, {2} CLR versions detected.",
                _dumpFile, _target.Architecture, _target.ClrVersions.Count);
            _context.DumpFile = _dumpFile;
        }

        private void AttachToProcess()
        {
            VerifyTargetProcessArchitecture();

            _target = DataTarget.AttachToProcess(_processId, ATTACH_TIMEOUT, AttachFlag.Passive);
            _context.WriteInfo("Attached to process {0}, architecture {1}, {2} CLR versions detected.",
                _processId, _target.Architecture, _target.ClrVersions.Count);
            _context.ProcessId = _processId;
        }

        private void CreateRuntimeInner(string dacLocation, ClrInfo clrInfo)
        {
            // FIXME This is a temporary patch for .NET 4.6. The DataTarget.CreateRuntime
            // code incorrectly detects .NET 4.6 and creates a LegacyRuntime instead of the
            // V45Runtime. The result is that the runtime fails to initialize. This was already
            // fixed in a PR https://github.com/Microsoft/dotnetsamples/pull/17 to ClrMD, 
            // but wasn't merged yet.
            string dacFileNoExt = Path.GetFileNameWithoutExtension(dacLocation);
            if (dacFileNoExt.Contains("mscordacwks") &&
                clrInfo.Version.Major == 4 &&
                clrInfo.Version.Minor >= 6)
            {
                Type dacLibraryType = typeof(DataTarget).Assembly.GetType("Microsoft.Diagnostics.Runtime.DacLibrary");
                object dacLibrary = Activator.CreateInstance(dacLibraryType, _target, dacLocation);
                Type v45RuntimeType = typeof(DataTarget).Assembly.GetType("Microsoft.Diagnostics.Runtime.Desktop.V45Runtime");
                object runtime = Activator.CreateInstance(v45RuntimeType, _target, dacLibrary);
                _context.Runtime = (ClrRuntime)runtime;
            }
            else
            {
                _context.Runtime = _target.CreateRuntime(dacLocation);
            }
        }

        private void CreateRuntime()
        {
            ClrInfo clrInfo = _target.ClrVersions[_clrVersionIndex];
            string dacLocation = clrInfo.TryDownloadDac();
            _context.WriteInfo("Using Data Access DLL at: " + dacLocation);
            _context.DacLocation = dacLocation;
            CreateRuntimeInner(dacLocation, clrInfo);
            _context.Heap = _context.Runtime.GetHeap();
            _target.DefaultSymbolNotification = new SymbolNotification(_context);
        }

        private void SetupSymPath()
        {
            string symPath = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH");
            _context.WriteInfo("Symbol path: " + symPath);
            _target.AppendSymbolPath(symPath);
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
                _context.WriteInfo("#{0} Flavor: {1}, Version: {2}", i, clrVersion.Flavor, clrVersion.Version);
            }
            if (_clrVersionIndex < 0 || _clrVersionIndex >= _target.ClrVersions.Count)
            {
                Bail("The ordinal number of the CLR to interact with is not valid. {0} specified, valid values are 0-{1}.",
                    _clrVersionIndex, _target.ClrVersions.Count - 1);
            }
            if (_target.ClrVersions.Count > 1)
            {
                _context.WriteInfo("The rest of this session will interact with CLR version #{0} ({1}). Change using --clrVersion.",
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
