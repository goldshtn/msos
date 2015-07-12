using CommandLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
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

        private Parser _commandParser;
        private Type[] _allCommandTypes;

        public CommandExecutionContext()
        {
            Aliases = new Dictionary<string, string>();
            _commandParser = new Parser(ps =>
            {
                ps.CaseSensitive = false;
                ps.HelpWriter = Console.Out;
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
                commandToExecute.Execute(this);
            }
        }

        public void WriteLine(string format, params object[] args)
        {
            Printer.WriteCommandOutput(format, args);
        }

        public void WriteLine(string value)
        {
            Printer.WriteCommandOutput(value);
        }

        public void WriteError(string format, params object[] args)
        {
            Printer.WriteError(format, args);
        }

        public void WriteError(string value)
        {
            Printer.WriteError(value);
        }

        public void WriteWarning(string format, params object[] args)
        {
            Printer.WriteWarning(format, args);
        }

        public void WriteWarning(string value)
        {
            Printer.WriteWarning(value);
        }

        public void WriteInfo(string format, params object[] args)
        {
            Printer.WriteInfo(format, args);
        }

        public void WriteInfo(string value)
        {
            Printer.WriteInfo(value);
        }

        public void Dispose()
        {
            Printer.Dispose();
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
