using CommandLine;
using Microsoft.Diagnostics.Runtime;
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

        private Parser _commandParser;
        private Type[] _allCommandTypes;
        private Dictionary<Tuple<string, int>, List<ClrType>> _typesByModuleAndMDToken;
        
        private List<string> _temporaryAliases = new List<string>();
        private const int WarnThresholdCountOfTemporaryAliases = 100;

        public CommandExecutionContext(TextWriter helpWriter = null)
        {
            SymbolCache = new SymbolCache();
            Aliases = new Dictionary<string, string>();
            Defines = new List<string>();
            HyperlinkOutput = true;
            _commandParser = new Parser(ps =>
            {
                ps.CaseSensitive = false;
                ps.HelpWriter = helpWriter ?? Console.Out;
            });
            _allCommandTypes = GetAllCommandTypes();
        }

        public ClrThread CurrentThread
        {
            get
            {
                return Runtime.Threads.FirstOrDefault(t => t.ManagedThreadId == CurrentManagedThreadId);
            }
        }

        public TargetType TargetType
        {
            get
            {
                return String.IsNullOrEmpty(DumpFile) ? TargetType.LiveProcess : TargetType.DumpFile;
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

            // The IgnoreUnknownArguments option is not yet available in the parser, so it tries
            // to eagerly parse alias commands. If the alias command itself contains things that look
            // like arguments, such as --type, the parser will erroneously think that it's an 
            // argument to the .newalias command, and not to the alias command. The same thing is
            // going on with the !hq command, where the query could contain -- and - symbols and it
            // crashes the parser. So, we give these two commands special treatment here, with the
            // hope there will be a more decent solution in the future.
            if (parts[0] == "!hq" && parts.Length >= 2)
            {
                commandToExecute = new HeapQuery() { OutputFormat = parts[1], Query = parts.Skip(2) };
            }
            else if (parts[0] == ".newalias" && parts.Length >= 2)
            {
                commandToExecute = new CreateAlias() { AliasName = parts[1], AliasCommand = parts.Skip(2) };
            }
            else
            {
                var parseResult = _commandParser.ParseArguments(parts, _allCommandTypes);
                var parsed = parseResult as Parsed<object>;
                if (parsed == null)
                    return;
                commandToExecute = (ICommand)parsed.Value;
            }

            using (new TimeAndMemory(displayDiagnosticInformation, Printer))
            {
                try
                {
                    commandToExecute.Execute(this);
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

        public DataTarget CreateDbgEngTarget()
        {
            if (String.IsNullOrEmpty(DumpFile))
                throw new InvalidOperationException("DbgEng targets can be created only for dump files at this point.");

            var target = DataTarget.LoadCrashDump(DumpFile, CrashDumpReader.DbgEng);
            target.AppendSymbolPath(SymbolPath);
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

        // TODO Can offer a command that runs an arbitrary WinDbg command by creating
        // a DbgEng target and then using the appropriate IDebug* method.

        public void Dispose()
        {
            Printer.Dispose();
        }

        private string AddTemporaryAlias(string command)
        {
            string alias = String.Format("a{0}", _temporaryAliases.Count);
            Aliases.Add(alias, command);
            _temporaryAliases.Add(alias);
            return alias;
        }

        private static Type[] GetAllCommandTypes()
        {
            return (from type in Assembly.GetExecutingAssembly().GetTypes()
                    where typeof(ICommand).IsAssignableFrom(type)
                    select type
                    ).ToArray();
        }
    }
}
