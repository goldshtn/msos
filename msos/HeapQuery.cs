using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb("!hq", HelpText = "Runs a query over heap objects and prints the results. Useful helpers include AllObjects(), ObjectsOfType(\"TypeName\"), AllClasses(), and Class(\"TypeName\"). Special properties on objects include __Type and __Size; special properties on classes include __Fields and __StaticFields.")]
    class HeapQuery : ICommand
    {
        public const string TabularOutputFormat = "tabular";
        public const string JsonOutputFormat = "json";

        private static string[] OutputFormats = { "--" + TabularOutputFormat, "--" + JsonOutputFormat };

        // This is done in a round-about way (instead of using [Option]) because we are parsing the arguments
        // manually at this point. The CommandLineParser isn't flexible enough. See comment in
        // CommandExecutionContext.ExecuteOneCommand for an explanation.
        [Value(0, Required = true, HelpText = "The output format.")]
        public string OutputFormat { get; set; }

        [Value(1, Required = true)]
        public IEnumerable<string> Query { get; set; }

        private class QueryOutputWriter : TextWriter
        {
            private CommandExecutionContext _context;
            private StringBuilder _buffer = new StringBuilder();

            public QueryOutputWriter(CommandExecutionContext context)
            {
                _context = context;
            }

            public override void Write(char value)
            {
                _buffer.Append(value);
                if (value == '\n')
                {
                    _context.WriteLine(_buffer.ToString().TrimEnd('\r', '\n'));
                    _buffer.Clear();
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (_buffer.Length != 0)
                {
                    _context.WriteLine(_buffer.ToString());
                    _buffer.Clear();
                }
                RemotingServices.Disconnect(this);
                base.Dispose(disposing);
            }

            public override Encoding Encoding
            {
                get { return Encoding.UTF8; }
            }

            public override object InitializeLifetimeService()
            {
                return null;
            }
        }

        public void Execute(CommandExecutionContext context)
        {
            if (!OutputFormats.Contains(OutputFormat))
            {
                context.WriteError("Unknown output format '{0}'. Output format must be one of the following: {1}",
                    OutputFormat, String.Join(", ", OutputFormats));
                return;
            }

            AppDomain appDomain = AppDomain.CreateDomain("RunQueryAppDomain");

            using (QueryOutputWriter writer = new QueryOutputWriter(context))
            {
                object[] arguments;
                if (context.ProcessId != 0)
                {
                    arguments = new object[] { context.ProcessId, context.DacLocation, writer };
                }
                else
                {
                    arguments = new object[] { context.DumpFile, context.DacLocation, writer };
                }
                using (RunInSeparateAppDomain runner = (RunInSeparateAppDomain)appDomain.CreateInstanceAndUnwrap(
                    typeof(RunInSeparateAppDomain).Assembly.FullName,
                    typeof(RunInSeparateAppDomain).FullName,
                    false, System.Reflection.BindingFlags.CreateInstance, null,
                    arguments, null, null
                    )
                )
                {
                    try
                    {
                        runner.RunQuery(OutputFormat.Substring(2), String.Join(" ", Query.ToArray()));
                    }
                    catch (Exception ex)
                    {
                        // Catching everything here because the input is user-controlled, so we can have 
                        // compilation errors, dynamic binder errors, and a variety of other things I haven't
                        // even thought of yet.
                        context.WriteError(ex.Message);
                    }
                }
            }

            AppDomain.Unload(appDomain);
        }
    }
}
