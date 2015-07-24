using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    static class ClrThreadExtensions
    {
        public static bool IsThreadPoolThread(this ClrThread thread)
        {
            return thread.IsThreadpoolCompletionPort || thread.IsThreadpoolGate || thread.IsThreadpoolTimer || thread.IsThreadpoolWait || thread.IsThreadpoolWorker;
        }

        public static string ApartmentDescription(this ClrThread thread)
        {
            if (thread.IsMTA)
                return "MTA";
            if (thread.IsSTA)
                return "STA";
            return "None";
        }

        public static string SpecialDescription(this ClrThread thread)
        {
            if (thread.IsDebuggerHelper)
                return "DbgHelper";
            if (thread.IsFinalizer)
                return "Finalizer";
            if (thread.IsGC)
                return "GC";
            if (thread.IsShutdownHelper)
                return "ShutdownHelper";
            if (thread.IsAborted)
                return "Aborted";
            if (thread.IsAbortRequested)
                return "AbortRequested";
            if (thread.IsUnstarted)
                return "Unstarted";
            if (thread.IsUserSuspended)
                return "Suspended";
            return "";
        }

        public static void WriteCurrentStackTraceToContext(this ClrThread thread, CommandExecutionContext context, bool displayArgumentsAndLocals)
        {
            thread.WriteStackTraceToContext(thread.StackTrace, context, displayArgumentsAndLocals);
        }

        public static void WriteCurrentExceptionStackTraceToContext(this ClrThread thread, CommandExecutionContext context, bool displayArgumentsAndLocals)
        {
            thread.WriteStackTraceToContext(thread.CurrentException.StackTrace, context, displayArgumentsAndLocals);
        }

        public static void WriteStackTraceToContext(this ClrThread thread, IList<ClrStackFrame> stackTrace, CommandExecutionContext context, bool displayArgumentsAndLocals)
        {
            FrameArgumentsAndLocals[] argsAndLocals = displayArgumentsAndLocals ?
                GetFrameArgumentsAndLocals(thread, context) :
                new FrameArgumentsAndLocals[0];

            context.WriteLine("{0,-20} {1,-20} {2}", "SP", "IP", "Function");
            foreach (var frame in stackTrace)
            {
                var sourceLocation = context.SymbolCache.GetFileAndLineNumberSafe(frame);
                context.WriteLine("{0,-20:X16} {1,-20:X16} {2} {3}",
                    frame.StackPointer, frame.InstructionPointer,
                    frame.DisplayString,
                    sourceLocation == null ? "" : String.Format("[{0}:{1},{2}]", sourceLocation.FilePath, sourceLocation.LineNumber, sourceLocation.ColStart));

                var frameArgsAndLocals = argsAndLocals.FirstOrDefault(
                    al => al.MethodName == frame.DisplayString);
                if (frameArgsAndLocals != null)
                {
                    Action<string, ArgumentOrLocal> action = (which, argOrLocal) =>
                    {
                        argOrLocal.PopulateActualType(context.Heap);
                        context.Write("  {0} {1} = {2} ({3}, size {4}) ",
                            which, argOrLocal.Name, argOrLocal.ValueRaw(),
                            argOrLocal.DynamicTypeName, argOrLocal.Size);
                        if (argOrLocal.ObjectAddress != 0)
                        {
                            context.WriteLink("",
                                String.Format("!do {0:x16}", argOrLocal.ObjectAddress));
                        }
                        context.WriteLine();
                    };
                    frameArgsAndLocals.Arguments.ForEach(a => action("arg", a));
                    frameArgsAndLocals.LocalVariables.ForEach(l => action("lcl", l));
                }
            }
        }

        class ArgumentOrLocal
        {
            public string Name;
            public string StaticTypeName;
            public ulong Size;
            public byte[] Value;
            public ulong ObjectAddress; // If it's a reference type
            public bool ProbablyReferenceType;

            private ClrType _actualType;

            public void PopulateActualType(ClrHeap heap)
            {
                if (!ProbablyReferenceType)
                    return;

                _actualType = heap.GetObjectType(ObjectAddress);
            }

            public string DynamicTypeName
            {
                get { return _actualType != null ? _actualType.Name : StaticTypeName; }
            }

            public string ValueRaw()
            {
                if (_actualType != null)
                {
                    if (_actualType.IsObjectReference && !_actualType.IsString)
                        return String.Format("{0:x16}", ObjectAddress);
                    
                    return _actualType.GetValue(ObjectAddress).ToStringOrNull();
                }

                if (Value == null || Value.Length == 0)
                    return "<unavailable>";

                StringBuilder result = new StringBuilder();
                foreach (byte b in Value)
                    result.AppendFormat("{0:x2} ", b);
                return result.ToString();
            }
        }

        class FrameArgumentsAndLocals
        {
            public string MethodName;
            public List<ArgumentOrLocal> Arguments = new List<ArgumentOrLocal>();
            public List<ArgumentOrLocal> LocalVariables = new List<ArgumentOrLocal>();
        }

        private static FrameArgumentsAndLocals[] GetFrameArgumentsAndLocals(
            ClrThread thread, CommandExecutionContext context)
        {
            IXCLRDataProcess ixclrDataProcess = context.Runtime.GetCLRDataProcess();

            object tmp;
            HR.Verify(ixclrDataProcess.GetTaskByOSThreadID(thread.OSThreadId, out tmp));

            IXCLRDataTask task = (IXCLRDataTask)tmp;
            HR.Verify(task.CreateStackWalk(0xf /*all flags*/, out tmp));

            IXCLRDataStackWalk stackWalk = (IXCLRDataStackWalk)tmp;

            List<FrameArgumentsAndLocals> results = new List<FrameArgumentsAndLocals>();
            while (HR.S_OK == stackWalk.Next())
            {
                if (HR.Failed(stackWalk.GetFrame(out tmp)))
                    continue;

                IXCLRDataFrame frame = (IXCLRDataFrame)tmp;

                StringBuilder methodName = new StringBuilder(1024);
                uint methodNameLen;
                if (HR.Failed(frame.GetCodeName(0 /*default flags*/, (uint)methodName.Capacity, out methodNameLen, methodName)))
                    continue;

                uint numArgs, numLocals;
                if (HR.Failed(frame.GetNumArguments(out numArgs)))
                    numArgs = 0;
                if (HR.Failed(frame.GetNumLocalVariables(out numLocals)))
                    numLocals = 0;

                FrameArgumentsAndLocals frameArgsLocals = new FrameArgumentsAndLocals()
                {
                    MethodName = methodName.ToString()
                };
                for (uint argIdx = 0; argIdx < numArgs; ++argIdx)
                {
                    StringBuilder argName = new StringBuilder(1024);
                    uint argNameLen;
                    if (HR.Failed(frame.GetArgumentByIndex(argIdx, out tmp, (uint)argName.Capacity, out argNameLen, argName)))
                        continue;

                    var arg = new ArgumentOrLocal() { Name = argName.ToString() };
                    FillValue(arg, (IXCLRDataValue)tmp);
                    frameArgsLocals.Arguments.Add(arg);
                }

                for (uint lclIdx = 0; lclIdx < numLocals; ++lclIdx)
                {
                    StringBuilder lclName = new StringBuilder(1024);
                    uint lclNameLen;
                    if (HR.Failed(frame.GetLocalVariableByIndex(lclIdx, out tmp, (uint)lclName.Capacity, out lclNameLen, lclName)))
                        continue;

                    var lcl = new ArgumentOrLocal() { Name = lclName.ToString() };
                    FillValue(lcl, (IXCLRDataValue)tmp);
                    frameArgsLocals.LocalVariables.Add(lcl);
                }

                results.Add(frameArgsLocals);
            }
            
            return results.ToArray();
        }

        private static void FillValue(ArgumentOrLocal argOrLocal, IXCLRDataValue value)
        {
            object tmp;

            ulong size;
            if (HR.Failed(value.GetSize(out size)))
                size = 0; // When the value is unavailable, GetSize fails; consider it 0

            bool probablyReferenceType = false;
            int getTypeHr = value.GetType(out tmp);
            if (getTypeHr == HR.S_FALSE)
            {
                // For reference types, GetType returns S_FALSE and we need to call GetAssociatedType
                // to retrieve the type that the reference points to.
                getTypeHr = value.GetAssociatedType(out tmp);
                probablyReferenceType = (getTypeHr == HR.S_OK);
            }

            if (getTypeHr != HR.S_OK)
                return;

            IXCLRDataTypeInstance typeInstance = (IXCLRDataTypeInstance)tmp;
            StringBuilder typeName = new StringBuilder(2048);
            uint typeNameLen;
            if (HR.Failed(typeInstance.GetName(0 /*CLRDATA_GETNAME_DEFAULT*/, (uint)typeName.Capacity, out typeNameLen, typeName)))
                return;

            argOrLocal.Size = size;
            argOrLocal.StaticTypeName = typeName.ToString();

            // Calling IXCLRDataTypeInstance::GetFlags doesn't tell us if it's a reference type,
            // a value type, or something else entirely. It just always returns 0 as the flag (and S_OK).
            // So we have to rely on the heuristic 'probablyReferenceType' to decide.
            
            if (size == 0)
                return;

            argOrLocal.Value = new byte[size];
            uint dataSize;
            if (HR.Failed(value.GetBytes((uint)argOrLocal.Value.Length, out dataSize, argOrLocal.Value)))
                argOrLocal.Value = null;

            if (probablyReferenceType)
            {
                argOrLocal.ObjectAddress = Environment.Is64BitProcess ? 
                    BitConverter.ToUInt64(argOrLocal.Value, 0) :
                    BitConverter.ToUInt32(argOrLocal.Value, 0);
                argOrLocal.ProbablyReferenceType = true;
            }

            // TODO Handle value types by creating a !do --type link, or for primitives,
            // just display the value inline.
        }
    }
}
