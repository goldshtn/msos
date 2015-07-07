using Microsoft.CSharp;
using Microsoft.Diagnostics.Runtime;
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo(msos.RunInSeparateAppDomain.CompiledQueryAssemblyName)]

namespace msos
{
    internal class RunQueryContext
    {
        public ClrHeap Heap { get; set; }
    }

    internal interface IRunQuery
    {
        object Run();
    }

    [Serializable]
    public class RunFailedException : Exception
    {
        public RunFailedException(string message)
            : base(message)
        {
        }

        protected RunFailedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    class RunInSeparateAppDomain : MarshalByRefObject, IDisposable
    {
        internal const string CompiledQueryAssemblyName = "msos_CompiledQuery";
        const int AttachTimeout = 1000;
        const string CompiledQueryPlaceholder = "$$$QUERY$$$";
        const string CompiledQueryTemplate = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using msos;

[assembly: InternalsVisibleTo(""msos"")]

internal class RunQuery : IRunQuery
{
    private RunQueryContext _context;

    public RunQuery(RunQueryContext context)
    {
        _context = context;
    }

    private IEnumerable<ulong> AllObjects()
    {
        return _context.Heap.EnumerateObjects();
    }

    public object Run()
    {
        return " + CompiledQueryPlaceholder + @";
    }
}
";

        private TextWriter _writer;
        private ClrHeap _heap;
        private DataTarget _target;

        private void CreateRuntime(string dacLocation)
        {
            ClrRuntime runtime = _target.CreateRuntime(dacLocation);
            _heap = runtime.GetHeap();
        }

        public RunInSeparateAppDomain(string dumpFile, string dacLocation, TextWriter writer)
        {
            _target = DataTarget.LoadCrashDump(dumpFile, CrashDumpReader.ClrMD);
            CreateRuntime(dacLocation);
            _writer = writer;
        }

        public RunInSeparateAppDomain(int pid, string dacLocation, TextWriter writer)
        {
            _target = DataTarget.AttachToProcess(pid, AttachTimeout, AttachFlag.Passive);
            CreateRuntime(dacLocation);
            _writer = writer;
        }

        public void RunQuery(string query)
        {
            var options = new CompilerParameters();
            options.ReferencedAssemblies.Add(typeof(Enumerable).Assembly.Location);
            options.ReferencedAssemblies.Add(Assembly.GetExecutingAssembly().Location);
            options.ReferencedAssemblies.Add(typeof(ClrHeap).Assembly.Location);
            options.CompilerOptions = "/optimize+";
            options.OutputAssembly = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                CompiledQueryAssemblyName) + ".dll";

            string source = CompiledQueryTemplate.Replace(CompiledQueryPlaceholder, query);

            var compiler = new CSharpCodeProvider();
            CompilerResults results = compiler.CompileAssemblyFromSource(options, source);

            if (results.Errors.HasErrors)
            {
                throw new RunFailedException(
                    String.Format("Query compilation failed with {0} errors:" + Environment.NewLine + "{1}",
                    results.Errors.Count,
                    String.Join(Environment.NewLine, (from error in results.Errors.Cast<CompilerError>() select error.ToString()).ToArray())
                    ));
            }

            Type compiledQueryType = results.CompiledAssembly.GetType("RunQuery");
            IRunQuery runQuery = (IRunQuery)Activator.CreateInstance(
                compiledQueryType, new RunQueryContext { Heap = _heap });
            
            object result = runQuery.Run();
            IEnumerable enumerable = result as IEnumerable;
            ulong rowCount = 0;
            if (enumerable != null)
            {
                foreach (var obj in enumerable)
                {
                    if (obj is ulong)
                    {
                        _writer.WriteLine(((ulong)obj).ToString("x16"));
                    }
                    else
                    {
                        _writer.WriteLine(obj.ToString());
                    }
                    ++rowCount;
                }
            }
            else
            {
                _writer.WriteLine(result.ToString());
                ++rowCount;
            }
            _writer.WriteLine("Rows: {0}", rowCount);
        }

        public void Dispose()
        {
            _target.Dispose();
        }
    }
}
