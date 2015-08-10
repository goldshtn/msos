using CmdLine;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    class CommandExecutionContext : IDisposable
    {
        public bool ShouldQuit { get; set; }
        public ClrRuntime Runtime { get; set; }
        public int CurrentManagedThreadId { get; set; }
        public string DumpFile { get; set; }
        public int ProcessId { get; set; }
        public string DacLocation { get; set; }
        public HeapIndex HeapIndex { get; set; }
        public ClrHeap Heap { get; set; }
        public IPrinter Printer { get; set; }
        public IDictionary<string, string> Aliases { get; private set; }
        public bool HyperlinkOutput { get; set; }
        public SymbolCache SymbolCache { get; private set; }
        public List<string> Defines { get; private set; }
        public string SymbolPath { get; set; }
        public ClrInfo ClrVersion { get; set; }
        public TargetType TargetType { get; set; }

        private CmdLineParser _commandParser;
        private Type[] _allCommandTypes;
        private Dictionary<Tuple<string, int>, List<ClrType>> _typesByModuleAndMDToken;
        private DataTarget _dbgEngDataTarget;
        private List<string> _temporaryAliases = new List<string>();
        private const int WarnThresholdCountOfTemporaryAliases = 100;

        public CommandExecutionContext(TextWriter helpWriter = null)
        {
            SymbolCache = new SymbolCache();
            Aliases = new Dictionary<string, string>();
            Defines = new List<string>();
            HyperlinkOutput = true;
            _commandParser = new CmdLineParser(helpWriter);
            _allCommandTypes = GetAllCommandTypes();
        }

        public ClrThread CurrentThread
        {
            get
            {
                return Runtime.Threads.FirstOrDefault(t => t.ManagedThreadId == CurrentManagedThreadId);
            }
        }

        public bool IsInDbgEngNativeMode { get { return _dbgEngDataTarget != null; } }

        public string Prompt
        {
            get
            {
                if (IsInDbgEngNativeMode)
                    return "dbgeng> ";

                return String.Format("{0}> ", CurrentManagedThreadId);
            }
        }

        public void ExecuteCommand(string inputCommand, bool displayDiagnosticInformation = false)
        {
            var commands = inputCommand.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var command in commands)
            {
                ExecuteOneCommand(command, displayDiagnosticInformation);
            }
        }

        public void ExecuteOneCommand(string command, bool displayDiagnosticInformation = false)
        {
            string[] parts = command.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return;

            if (parts[0] == "#")
                return; // Lines starting with # are comments

            ICommand commandToExecute;

            if (IsInDbgEngNativeMode && parts[0] != "q")
            {
                commandToExecute = new DbgEngCommand() { Command = command };
            }
            else
            {
                var parseResult = _commandParser.Parse(_allCommandTypes, command);
                if (!parseResult.Success)
                {
                    WriteError(parseResult.Error);
                    return;
                }
                if (parseResult.Value == null)
                    return;
                commandToExecute = (ICommand)parseResult.Value;
            }

            using (new TimeAndMemory(displayDiagnosticInformation, Printer))
            {
                try
                {
                    if (!IsCommandIsSupportedForThisTarget(commandToExecute.GetType()))
                    {
                        WriteError("This command is not supported for the current target type: '{0}'", TargetType);
                    }
                    else
                    {
                        commandToExecute.Execute(this);
                    }
                }
                catch (Exception ex)
                {
                    // Commands can throw exceptions occasionally. It's dangerous to continue
                    // after an arbitrary exception, but some of them are perfectly benign. We are
                    // taking the risk because there is no critical state that could become corrupted
                    // as a result of continuing.
                    WriteError("Exception during command execution -- {0}: '{1}'", ex.GetType().Name, ex.Message);
                    WriteError("\n" + ex.StackTrace);
                    WriteError("Proceed at your own risk, or restart the debugging session.");
                }
            }
            if (HyperlinkOutput && _temporaryAliases.Count > WarnThresholdCountOfTemporaryAliases)
            {
                WriteWarning("Hyperlinks are enabled. You currently have {0} temporary aliases. " +
                    "Use .clearalias --temporary to clear them.", _temporaryAliases.Count);
            }
            Printer.CommandEnded();
        }

        public void RemoveTemporaryAliases()
        {
            foreach (var alias in _temporaryAliases)
            {
                Aliases.Remove(alias);
            }
            _temporaryAliases.Clear();
        }

        public void Write(string format, params object[] args)
        {
            Printer.WriteCommandOutput(format, args);
        }

        public void Write(string value)
        {
            Printer.WriteCommandOutput(value);
        }

        public void WriteLine(string format, params object[] args)
        {
            Printer.WriteCommandOutput(format + Environment.NewLine, args);
        }

        public void WriteLine(string value)
        {
            Printer.WriteCommandOutput(value + Environment.NewLine);
        }

        public void WriteLine()
        {
            Printer.WriteCommandOutput(Environment.NewLine);
        }

        public void WriteError(string format, params object[] args)
        {
            Printer.WriteError(format + Environment.NewLine, args);
        }

        public void WriteError(string value)
        {
            Printer.WriteError(value + Environment.NewLine);
        }

        public void WriteWarning(string format, params object[] args)
        {
            Printer.WriteWarning(format + Environment.NewLine, args);
        }

        public void WriteWarning(string value)
        {
            Printer.WriteWarning(value + Environment.NewLine);
        }

        public void WriteInfo(string format, params object[] args)
        {
            Printer.WriteInfo(format + Environment.NewLine, args);
        }

        public void WriteInfo(string value)
        {
            Printer.WriteInfo(value + Environment.NewLine);
        }

        public void WriteLink(string text, string command)
        {
            if (HyperlinkOutput)
            {
                if (Printer.HasNativeHyperlinkSupport)
                {
                    Printer.WriteLink(text, command);
                }
                else
                {
                    // To work around the fact that we don't always have native
                    // hyperlink output (like HTML), we create a temporary alias
                    // for each link. The user can then execute the alias, which 
                    // is like "clicking" the link.
                    string alias = AddTemporaryAlias(command);
                    Write(text + " ");
                    Printer.WriteLink(String.Format("[{0}]", alias), command);
                }
            }
            else
            {
                Write(text);
            }
        }

        /// <summary>
        /// Creates a persistent DbgEng DataTarget that can be used to execute multiple
        /// commands (remembers state). While this DataTarget is in place, msos is placed
        /// in native DbgEng "mode", and accepts only DbgEng commands.
        /// </summary>
        public void EnterDbgEngNativeMode()
        {
            _dbgEngDataTarget = CreateDbgEngDataTargetImpl();
        }

        public void ExitDbgEngNativeMode()
        {
            _dbgEngDataTarget.Dispose();
            _dbgEngDataTarget = null;
        }

        /// <summary>
        /// Creates a temporary DbgEng DataTarget. It is used for a single command's
        /// execution, such as !lm or !mk, and disposed immediately thereafter. If there
        /// is already a persistent DbgEng DataTarget, i.e. the debugger is currently in 
        /// native DbgEng "mode", this method fails.
        /// </summary>
        public DataTarget CreateTemporaryDbgEngTarget()
        {
            if (_dbgEngDataTarget != null)
                throw new InvalidOperationException("There is already a persistent DbgEng DataTarget. Creating a temporary one is not allowed.");

            return CreateDbgEngDataTargetImpl();
        }

        public DataTarget NativeDbgEngTarget { get { return _dbgEngDataTarget; } }

        private DataTarget CreateDbgEngDataTargetImpl()
        {
            if (String.IsNullOrEmpty(DumpFile))
                throw new InvalidOperationException("DbgEng targets can be created only for dump files at this point.");

            var target = DataTarget.LoadCrashDump(DumpFile, CrashDumpReader.DbgEng);
            target.AppendSymbolPath(SymbolPath);

            var outputCallbacks = new OutputCallbacks(this);
            msos_IDebugClient5 client = (msos_IDebugClient5)target.DebuggerInterface;
            HR.Verify(client.SetOutputCallbacksWide(outputCallbacks));

            return target;
        }

        public ClrType GetTypeByMetadataToken(string moduleName, int mdTypeDefToken)
        {
            // The metadata token is not unique, and it repeats across modules. Turns out
            // sometimes it even repeats in the same module: all closed generic types share 
            // the same metadata token, and it's also the same token as the corresponding 
            // open generic type.
            if (_typesByModuleAndMDToken == null)
            {
                _typesByModuleAndMDToken = new Dictionary<Tuple<string, int>, List<ClrType>>();
                foreach (var type in Heap.EnumerateTypes())
                {
                    List<ClrType> list;
                    var key = new Tuple<string, int>(
                        Path.GetFileNameWithoutExtension(type.Module.Name),
                        (int)type.MetadataToken);
                    if (!_typesByModuleAndMDToken.TryGetValue(key, out list))
                    {
                        list = new List<ClrType>();
                        _typesByModuleAndMDToken.Add(key, list);
                    }
                    list.Add(type);
                }
            }
            
            List<ClrType> candidates;
            _typesByModuleAndMDToken.TryGetValue(
                new Tuple<string, int>(moduleName, mdTypeDefToken), out candidates);

            if (candidates == null || candidates.Count != 1)
                return null; // We don't know which one to pick

            return candidates[0];
        }

        public void Dispose()
        {
            Printer.Dispose();

            if (_dbgEngDataTarget != null)
                _dbgEngDataTarget.Dispose();
        }

        private string AddTemporaryAlias(string command)
        {
            string alias = String.Format("a{0}", _temporaryAliases.Count);
            Aliases.Add(alias, command);
            _temporaryAliases.Add(alias);
            return alias;
        }

        private Type[] GetAllCommandTypes()
        {
            return (from type in Assembly.GetExecutingAssembly().GetTypes()
                    where typeof(ICommand).IsAssignableFrom(type) && type != typeof(ICommand)
                    select type
                    ).ToArray();
        }

        private bool IsCommandIsSupportedForThisTarget(Type type)
        {
            var supportedTargetsAttr = type.GetCustomAttribute<SupportedTargetsAttribute>();
            if (supportedTargetsAttr == null)
                return false;

            return supportedTargetsAttr.SupportedTargets.Contains(TargetType);
        }
    }
}
