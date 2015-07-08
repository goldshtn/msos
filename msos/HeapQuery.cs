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
    [Verb("!hq", HelpText = "Runs a query over heap objects and prints the results. Examples: TODO")]
    class HeapQuery : ICommand
    {
        // This is a collection because we want to capture anything provided after
        // the initial !hq command. We then collect the parts back to a single string.
        [Value(0, Required = true)]
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
                        runner.RunQuery(String.Join(" ", Query.ToArray()));
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
