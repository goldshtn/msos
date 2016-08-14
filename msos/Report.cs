using CmdLine;
using Microsoft.Diagnostics.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    enum AnalysisResult
    {
        CompletedSuccessfully,
        InternalError
    }

    class ReportDocument
    {
        public DateTime AnalysisStartTime { get; } = DateTime.Now;
        public DateTime AnalysisEndTime { get; set; }
        public AnalysisResult AnalysisResult { get; set; } = AnalysisResult.CompletedSuccessfully;
        public List<IReportComponent> Components { get; } = new List<IReportComponent>();
    }

    interface IReportComponent
    {
        string Title { get; }
        bool Generate(CommandExecutionContext context);
    }

    class DumpInformationComponent : IReportComponent
    {
        public string Title { get; private set; }
        public string DumpType { get; private set; }

        public bool Generate(CommandExecutionContext context)
        {
            Title = Path.GetFileName(context.DumpFile);
            switch (context.TargetType)
            {
                case TargetType.DumpFile:
                    DumpType = "Full memory dump with heap";
                    break;
                case TargetType.DumpFileNoHeap:
                    DumpType = "Mini dump with no heap";
                    break;
                default:
                    DumpType = "Unsupported dump file type";
                    break;
            }
            return true;
        }
    }

    class RecommendationsComponent : IReportComponent
    {
        public string Title => "Issues and next steps";

        public bool Generate(CommandExecutionContext context)
        {
            return false;
        }
    }

    class UnhandledExceptionComponent : IReportComponent
    {
        public class ExceptionInfo
        {
            public string ExceptionType { get; set; }
            public string ExceptionMessage { get; set; }
            public List<string> StackFrames { get; set; }
            public ExceptionInfo InnerException { get; set; }
        }

        public string Title => "The process encountered an unhandled exception";
        public ExceptionInfo Exception { get; private set; }
        public uint OSThreadId { get; private set; }
        public int ManagedThreadId { get; private set; }
        public string ThreadName { get; private set; }

        public bool Generate(CommandExecutionContext context)
        {
            // TODO Do we care about Win32 exceptions as well, or only managed?
            //      Also makes sense to cross-check the current exception stack trace
            //      with the last event information, because the exception might actually
            //      be residual.
            var threadWithException = context.Runtime.Threads.FirstOrDefault(
                t => t.CurrentException != null
                );
            if (threadWithException == null)
                return false;

            OSThreadId = threadWithException.OSThreadId;
            ManagedThreadId = threadWithException.ManagedThreadId;
            ThreadName = threadWithException.SpecialDescription(); // TODO Get the actual name if possible

            var exception = threadWithException.CurrentException;
            var exceptionInfo = Exception = new ExceptionInfo();
            while (true)
            {
                exceptionInfo.ExceptionType = exception.Type.Name;
                exceptionInfo.ExceptionMessage = exception.Message;
                exceptionInfo.StackFrames = exception.StackTrace.Select(f => f.DisplayString).ToList();

                exception = exception.Inner;
                if (exception == null)
                    break;
                exceptionInfo.InnerException = new ExceptionInfo();
                exceptionInfo = exceptionInfo.InnerException;
            }
            return true;
        }
    }

    class LoadedModulesComponent : IReportComponent
    {
        public class LoadedModule
        {
            public string Name { get; set; }
            public ulong Size { get; set; }
            public string Path { get; set; }
            public string Version { get; set; }
            public bool IsManaged { get; set; }
        }

        public string Title { get { return "Loaded modules"; } }
        public List<LoadedModule> Modules { get; } = new List<LoadedModule>();

        public bool Generate(CommandExecutionContext context)
        {
            foreach (var module in context.Runtime.DataTarget.EnumerateModules())
            {
                var loadedModule = new LoadedModule
                {
                    Name = Path.GetFileName(module.FileName),
                    Size = module.FileSize,
                    Path = module.FileName,
                    Version = module.Version.ToString(),
                    IsManaged = module.IsManaged
                };
                Modules.Add(loadedModule);
            }
            return true;
        }
    }

    class ThreadStacksComponent : IReportComponent
    {
        public class StackFrame
        {
            public string Module { get; set; }
            public string Method { get; set; }
            public string SourceFileName { get; set; }
            public uint SourceLineNumber { get; set; }
        }

        public class StackTrace
        {
            public uint OSThreadId { get; set; }
            public int ManagedThreadId { get; set; }
            public List<StackFrame> Frames { get; } = new List<StackFrame>();
        }

        public string Title => "Thread stacks";
        public List<StackTrace> Stacks { get; } = new List<StackTrace>();

        public bool Generate(CommandExecutionContext context)
        {
            using (var target = context.CreateTemporaryDbgEngTarget())
            {
                var stackTraces = new UnifiedStackTrace(target.DebuggerInterface, context);
                foreach (var thread in stackTraces.Threads)
                {
                    var stackTrace = stackTraces.GetStackTrace(thread.Index);
                    var st = new StackTrace
                    {
                        OSThreadId = thread.OSThreadId,
                        ManagedThreadId = thread.ManagedThread?.ManagedThreadId ?? -1
                    };
                    foreach (var frame in stackTrace)
                    {
                        st.Frames.Add(new StackFrame
                        {
                            Module = frame.Module,
                            Method = frame.Method,
                            SourceFileName = frame.SourceFileName,
                            SourceLineNumber = frame.SourceLineNumber
                        });
                    }
                    Stacks.Add(st);
                }
            }
            return true;
        }
    }

    class LocksAndWaitsComponent : IReportComponent
    {
        public class LockInfo
        {
            public string Reason { get; set; }
            public ulong Object { get; set; }
            public string ObjectType { get; set; }
            public List<ThreadInfo> OwnerThreads { get; } = new List<ThreadInfo>();
            public List<ThreadInfo> WaitingThreads { get; } = new List<ThreadInfo>();
        }

        public class ThreadInfo
        {
            public uint OSThreadId { get; set; }
            public int ManagedThreadId { get; set; }
            public List<LockInfo> Locks { get; } = new List<LockInfo>();
        }

        public string Title => "Locks and waits";
        public List<ThreadInfo> Threads { get; } = new List<ThreadInfo>();

        private static ThreadInfo ThreadInfoFromThread(ClrThread thread)
        {
            return new ThreadInfo
            {
                OSThreadId = thread.OSThreadId,
                ManagedThreadId = thread.ManagedThreadId
            };
        }

        public bool Generate(CommandExecutionContext context)
        {
            foreach (var thread in context.Runtime.Threads)
            {
                if (thread.BlockingObjects.Count == 0)
                    continue;

                var ti = ThreadInfoFromThread(thread);
                foreach (var blockingObject in thread.BlockingObjects)
                {
                    var li = new LockInfo
                    {
                        Reason = blockingObject.Reason.ToString(),
                        Object = blockingObject.Object,
                        ObjectType = context.Heap.GetObjectType(blockingObject.Object)?.Name
                    };
                    li.OwnerThreads.AddRange(blockingObject.Owners.Select(ThreadInfoFromThread));
                    li.WaitingThreads.AddRange(blockingObject.Waiters.Select(ThreadInfoFromThread));
                    ti.Locks.Add(li);
                }
                Threads.Add(ti);
            }
            return Threads.Any();
        }
    }

    class MemoryUsageComponent : IReportComponent
    {
        public string Title => "Memory usage";

        public bool Generate(CommandExecutionContext context)
        {
            return true;
        }
    }

    class TopMemoryConsumersComponent : IReportComponent
    {
        public string Title => "Top .NET memory consumers";

        public bool Generate(CommandExecutionContext context)
        {
            return true;
        }
    }

    class MemoryFragmentationComponent : IReportComponent
    {
        public string Title => "Memory fragmentation";

        public bool Generate(CommandExecutionContext context)
        {
            return true;
        }
    }

    class FinalizationComponent : IReportComponent
    {
        public string Title => "Finalization statistics";

        public bool Generate(CommandExecutionContext context)
        {
            return true;
        }
    }

    [Verb("report", HelpText = "Generate an automatic analysis report of the dump file with recommendations in a JSON format.")]
    [SupportedTargets(TargetType.DumpFile, TargetType.DumpFileNoHeap)]
    class Report : ICommand
    {
        [Option('f', Required = true, HelpText = "The name of the report file.")]
        public string FileName { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            var reportDocument = new ReportDocument();

            var components = from type in Assembly.GetExecutingAssembly().GetTypes()
                             where type.GetInterface(typeof(IReportComponent).FullName) != null
                             select (IReportComponent)Activator.CreateInstance(type);

            foreach (var component in components)
            {
                if (component.Generate(context))
                    reportDocument.Components.Add(component);

                // TODO Handle errors
            }

            reportDocument.AnalysisEndTime = DateTime.Now;

            string jsonReport = JsonConvert.SerializeObject(reportDocument, Formatting.Indented, new StringEnumConverter());
            File.WriteAllText(FileName, jsonReport);
        }
    }
}
