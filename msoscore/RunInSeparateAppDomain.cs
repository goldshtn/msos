using Microsoft.CSharp;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.RuntimeExt;
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo(msos.RunInSeparateAppDomain.CompiledQueryAssemblyName)]

namespace msos
{
    internal class RunQueryContext
    {
        public ClrHeap Heap { get; set; }
        public ClrRuntime Runtime { get; set; }

        public IEnumerable<dynamic> ObjectsOfType(string typeName)
        {
            return from obj in Heap.EnumerateObjects()
                   let type = Heap.GetObjectType(obj)
                   where type != null && typeName == type.Name
                   select Heap.GetDynamicObject(obj);
        }

        public IEnumerable<dynamic> AllObjects()
        {
            return from obj in Heap.EnumerateObjects()
                   select Heap.GetDynamicObject(obj);
        }

        public dynamic Class(string typeName)
        {
            return Heap.GetDynamicClass(typeName);
        }

        public IEnumerable<dynamic> AllClasses()
        {
            return from type in Heap.EnumerateTypes()
                   select Heap.GetDynamicClass(type.Name);
        }

        public dynamic Object(ulong address)
        {
            return Heap.GetDynamicObject(address);
        }

        public IEnumerable<dynamic> SubgraphOf(ulong address)
        {
            return from pair in Heap.SubgraphOf(address)
                   select Heap.GetDynamicObject(pair.Item1);
        }

        public IEnumerable<object> Dump(ulong address)
        {
            var type = Heap.GetObjectType(address);
            if (type == null || type.IsFree || String.IsNullOrEmpty(type.Name))
                yield break;

            foreach (var field in type.Fields)
            {
                var fieldTypeName = field.Type != null ? field.Type.Name : "<unknown>";
                yield return new { Name = field.Name, Type = fieldTypeName, Value = field.GetValue(address) };
            }
        }

        public IEnumerable<dynamic> ObjectsInSegment(int segmentIdx)
        {
            return from obj in Heap.Segments[segmentIdx].EnumerateObjects()
                   select Heap.GetDynamicObject(obj);
        }
    }

    internal interface IRunQuery
    {
        object Run();
    }

    [Serializable]
    class RunFailedException : Exception
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
        const int TotalWidth = 100;
        const string CompiledQueryPlaceholder = "$$$QUERY$$$";
        const string HelperDefinesPlaceholder = "$$$DEFINES$$$";
        const string CompiledQueryTemplate = @"
using Microsoft.CSharp;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using msos;

[assembly: InternalsVisibleTo(""msoscore"")]

internal class RunQuery : IRunQuery
{
    private RunQueryContext _context;

    public RunQuery(RunQueryContext context)
    {
        _context = context;
    }

    private IEnumerable<dynamic> AllObjects()
    {
        return _context.AllObjects();
    }

    private IEnumerable<dynamic> ObjectsOfType(string typeName)
    {
        return _context.ObjectsOfType(typeName);
    }

    private dynamic Class(string typeName)
    {
        return _context.Class(typeName);
    }

    private IEnumerable<dynamic> AllClasses()
    {
        return _context.AllClasses();
    }

    private dynamic Object(ulong address)
    {
        return _context.Object(address);
    }

    private IEnumerable<dynamic> ObjectEn(ulong address)
    {
        yield return Object(address);
    }

    private IEnumerable<dynamic> SubgraphOf(ulong address)
    {
        return _context.SubgraphOf(address);
    }

    private IEnumerable<object> Dump(ulong address)
    {
        return _context.Dump(address);
    }

    private IEnumerable<dynamic> ObjectsInSegment(int segmentIdx)
    {
        return _context.ObjectsInSegment(segmentIdx);
    }

    private ClrRuntime GetRuntime()
    {
        return _context.Runtime;
    }

    public object Run()
    {
        return (" + CompiledQueryPlaceholder + @");
    }

    " + HelperDefinesPlaceholder + @"
}
";

        private IPrinter _printer;
        private ClrHeap _heap;
        private ClrRuntime _runtime;
        private DataTarget _target;

        private void CreateRuntime(string dacLocation)
        {
            _runtime = _target.CreateRuntime(dacLocation);
            _heap = _runtime.GetHeap();
        }

        public RunInSeparateAppDomain(string dumpFile, string dacLocation, IPrinter printer)
        {
            _target = DataTarget.LoadCrashDump(dumpFile, CrashDumpReader.ClrMD);
            CreateRuntime(dacLocation);
            _printer = printer;
        }

        public RunInSeparateAppDomain(int pid, string dacLocation, IPrinter printer)
        {
            _target = DataTarget.AttachToProcess(pid, AttachTimeout, AttachFlag.Passive);
            CreateRuntime(dacLocation);
            _printer = printer;
        }

        public string RunQuery(string outputFormat, string query, List<string> defines)
        {
            var options = new CompilerParameters();
            options.ReferencedAssemblies.Add(typeof(Enumerable).Assembly.Location);
            options.ReferencedAssemblies.Add(Assembly.GetExecutingAssembly().Location);
            options.ReferencedAssemblies.Add(typeof(ClrHeap).Assembly.Location);
            options.ReferencedAssemblies.Add(typeof(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException).Assembly.Location);
            options.CompilerOptions = "/optimize+";

            string compilationOutputDir = Path.Combine(Path.GetTempPath(), "msos_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(compilationOutputDir);
            options.OutputAssembly = Path.ChangeExtension(
                Path.Combine(compilationOutputDir, CompiledQueryAssemblyName),
                ".dll");

            string source = CompiledQueryTemplate.Replace(CompiledQueryPlaceholder, query);
            source = source.Replace(HelperDefinesPlaceholder,
                String.Join(Environment.NewLine + Environment.NewLine, defines.ToArray()));

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
                compiledQueryType, new RunQueryContext { Heap = _heap, Runtime = _runtime });
            
            object result = runQuery.Run();

            IObjectPrinter printer = null;
            switch ((HeapQueryOutputFormat)Enum.Parse(typeof(HeapQueryOutputFormat), outputFormat))
            {
                case HeapQueryOutputFormat.Tabular:
                    printer = new TabularObjectPrinter(_printer);
                    break;
                case HeapQueryOutputFormat.Json:
                    printer = new JsonObjectPrinter(_printer);
                    break;
                default:
                    throw new NotSupportedException(String.Format(
                        "The output format '{0}' is not supported", outputFormat));
            }

            IEnumerable enumerable = result as IEnumerable;
            ulong rowCount = 0;
            if (enumerable != null && !(result is string))
            {
                bool first = true;
                foreach (var obj in enumerable)
                {
                    if (obj is ulong)
                    {
                        _printer.WriteCommandOutput(((ulong)obj).ToString("x16") + Environment.NewLine);
                    }
                    else if (obj.IsAnonymousType())
                    {
                        if (first)
                        {
                            printer.PrintHeader(obj);
                            first = false;
                        }
                        printer.PrintContents(obj);
                    }
                    else
                    {
                        _printer.WriteCommandOutput(obj.ToString() + Environment.NewLine);
                    }
                    ++rowCount;
                }
            }
            else
            {
                _printer.WriteCommandOutput(result.ToString() + Environment.NewLine);
                ++rowCount;
            }
            _printer.WriteCommandOutput("Rows: {0}" + Environment.NewLine, rowCount);

            return compilationOutputDir;
        }

        interface IObjectPrinter
        {
            void PrintHeader(object obj);
            void PrintContents(object obj);
        }

        abstract class ObjectPrinterBase : IObjectPrinter
        {
            protected IPrinter Printer { get; private set; }

            protected ObjectPrinterBase(IPrinter printer)
            {
                Printer = printer;
            }

            public abstract void PrintHeader(object obj);
            public abstract void PrintContents(object obj);

            protected string RenderProperty(PropertyInfo prop, object obj)
            {
                object propVal = prop.GetValue(obj);
                if (propVal is ulong)
                {
                    return String.Format("{0:x16}", propVal);
                }
                else
                {
                    return propVal.ToStringOrNull();
                }
            }
        }

        class TabularObjectPrinter : ObjectPrinterBase
        {
            public TabularObjectPrinter(IPrinter printer)
                : base(printer)
            {
            }

            public override void PrintHeader(object obj)
            {
                var props = obj.GetType().GetProperties();
                int width = TotalWidth / props.Length;
                for (int i = 0; i < props.Length; ++i)
                {
                    // Do not restrict the width of the last property.
                    if (i == props.Length - 1)
                    {
                        Printer.WriteCommandOutput(props[i].Name.TrimEndToLength(width));
                    }
                    else
                    {
                        Printer.WriteCommandOutput("{0,-" + width + "}  ", props[i].Name.TrimEndToLength(width));
                    }
                }
                Printer.WriteCommandOutput(Environment.NewLine);
            }

            public override void PrintContents(object obj)
            {
                var props = obj.GetType().GetProperties();
                int width = TotalWidth / props.Length;
                for (int i = 0; i < props.Length; ++i)
                {
                    // Do not restrict the width of the last property.
                    if (i == props.Length - 1)
                    {
                        Printer.WriteCommandOutput(RenderProperty(props[i], obj));
                    }
                    else
                    {
                        Printer.WriteCommandOutput(
                            "{0,-" + width + "}  ",
                            RenderProperty(props[i], obj).TrimEndToLength(width));
                    }
                }
                Printer.WriteCommandOutput(Environment.NewLine);
            }
        }

        class JsonObjectPrinter : ObjectPrinterBase
        {
            public JsonObjectPrinter(IPrinter printer)
                : base(printer)
            {
            }

            public override void PrintHeader(object obj)
            {
            }

            public override void PrintContents(object obj)
            {
                Printer.WriteCommandOutput("{" + Environment.NewLine);
                var props = obj.GetType().GetProperties();
                foreach (var prop in props)
                {
                    Printer.WriteCommandOutput(
                        "  {0} = {1}" + Environment.NewLine,
                        prop.Name, RenderProperty(prop, obj));
                }
                Printer.WriteCommandOutput("}" + Environment.NewLine);
            }
        }

        public void Dispose()
        {
            _target.Dispose();
            RemotingServices.Disconnect(this);
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }
    }
}
