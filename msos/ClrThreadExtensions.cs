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
                new FrameArgumentsAndLocalsRetriever(thread, context).ArgsAndLocals :
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
                        context.Write("  {0} {1} {2:x16} = {3} ({4}, size {5}) ",
                            which, argOrLocal.Name, argOrLocal.Location,
                            argOrLocal.ValueRaw(), argOrLocal.DynamicTypeName,
                            argOrLocal.Size);
                        if (argOrLocal.ObjectAddress != 0)
                        {
                            context.WriteLink("",
                                String.Format("!do {0:x16}", argOrLocal.ObjectAddress));
                        }
                        else if (argOrLocal.HasNonTrivialValueToDisplay)
                        {
                            context.WriteLink("",
                                String.Format("!do {0:x16} --type {1}", argOrLocal.Location, argOrLocal.ClrType.Name));
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
            public ClrType ClrType;
            public ulong Location; // The arg/local's address, not value

            public bool IsReferenceType
            {
                get
                {
                    return ClrType != null ? ClrType.IsObjectReference : false;
                }
            }

            public bool HasNonTrivialValueToDisplay
            {
                get
                {
                    return Location != 0 && ClrType != null &&
                        ClrType.IsValueClass && !ClrType.HasSimpleValue;
                }
            }

            public string DynamicTypeName
            {
                get { return ClrType != null ? ClrType.Name : StaticTypeName; }
            }

            public string ValueRaw()
            {
                if (ClrType != null)
                {
                    // Display strings inline.
                    if (ClrType.IsString)
                        return ClrType.GetValue(ObjectAddress).ToStringOrNull();

                    // Display objects (non-strings) as an address.
                    // NOTE ObjectAddress could be 0 while the value is simply unavailable.
                    if (ClrType.IsObjectReference && Value != null)
                        return String.Format("{0:x16}", ObjectAddress);

                    if (HasNonTrivialValueToDisplay)
                        return "VALTYPE";

                    // Display primitive value types inline.
                    if (ClrType.HasSimpleValue && Location != 0)
                        return ClrType.GetValue(Location).ToStringOrNull();
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

        class FrameArgumentsAndLocalsRetriever
        {
            const int MaxNameSize = 1024;

            private CommandExecutionContext _context;
            private List<FrameArgumentsAndLocals> _results = new List<FrameArgumentsAndLocals>();

            public FrameArgumentsAndLocals[] ArgsAndLocals { get { return _results.ToArray(); } }

            public FrameArgumentsAndLocalsRetriever(ClrThread thread, CommandExecutionContext context)
            {
                _context = context;
                GetFrameArgumentsAndLocals(thread.OSThreadId);
            }

            private void GetFrameArgumentsAndLocals(uint osThreadId)
            {
                IXCLRDataProcess ixclrDataProcess = _context.Runtime.GetCLRDataProcess();

                object tmp;
                HR.Verify(ixclrDataProcess.GetTaskByOSThreadID(osThreadId, out tmp));

                IXCLRDataTask task = (IXCLRDataTask)tmp;
                HR.Verify(task.CreateStackWalk(0xf /*all flags*/, out tmp));

                IXCLRDataStackWalk stackWalk = (IXCLRDataStackWalk)tmp;

                while (HR.S_OK == stackWalk.Next())
                {
                    ProcessFrame(stackWalk);
                }
            }

            private void ProcessFrame(IXCLRDataStackWalk stackWalk)
            {
                object tmp;

                if (HR.Failed(stackWalk.GetFrame(out tmp)))
                    return;

                IXCLRDataFrame frame = (IXCLRDataFrame)tmp;

                StringBuilder methodName = new StringBuilder(MaxNameSize);
                uint methodNameLen;
                if (HR.Failed(frame.GetCodeName(0 /*default flags*/, (uint)methodName.Capacity, out methodNameLen, methodName)))
                    return;

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
                    StringBuilder argName = new StringBuilder(MaxNameSize);
                    uint argNameLen;

                    if (HR.Failed(frame.GetArgumentByIndex(argIdx, out tmp, (uint)argName.Capacity, out argNameLen, argName)))
                        continue;

                    var arg = new ArgumentOrLocal() { Name = argName.ToString() };
                    FillValue(arg, (IXCLRDataValue)tmp);
                    frameArgsLocals.Arguments.Add(arg);
                }

                for (uint lclIdx = 0; lclIdx < numLocals; ++lclIdx)
                {
                    StringBuilder lclName = new StringBuilder(MaxNameSize);
                    uint lclNameLen;
                    if (HR.Failed(frame.GetLocalVariableByIndex(lclIdx, out tmp, (uint)lclName.Capacity, out lclNameLen, lclName)))
                        continue;

                    // TODO The mscordacwks!ClrDataFrame::GetLocalVariableByIndex implementation never returns
                    // names for local variables. See https://github.com/dotnet/coreclr/blob/4cf8a6b082d9bb1789facd996d8265d3908757b2/src/debug/daccess/stack.cpp#L983.
                    // What we need to do instead is the following:
                    //   1) Get the PDB path for the module that contains the method
                    //   2) Get the IMetadataImport interface from ClrMD's ModuleInfo
                    //   3) Create a SymBinder and call GetReader to get an ISymbolReader for that IMetadataImport, module, and PDB path
                    //   4) Call ISymbolReader.GetMethod with the method's mdToken to get an ISymbolMethod
                    //   5) Enumerate its scopes recursively from ISymbolMethod.RootScope through its GetChildren()
                    //   6) In each scope, inspect the locals with ISymbolScope.GetLocals()
                    //   7) For each local, verify AddressKind == SymAddressKind.ILOffset and AddressField1 is the local variable index
                    //   8) Get the matching local's name
                    // It seems that getting the ISymbolReader is a time-consuming operation, so it's worthwhile to cache
                    // the ISymbolReader-s already obtained on a module basis.

                    var lcl = new ArgumentOrLocal() { Name = lclName.ToString() };
                    FillValue(lcl, (IXCLRDataValue)tmp);
                    frameArgsLocals.LocalVariables.Add(lcl);
                }

                _results.Add(frameArgsLocals);
            }

            private void FillValue(ArgumentOrLocal argOrLocal, IXCLRDataValue value)
            {
                object tmp;

                ulong size;
                if (HR.Failed(value.GetSize(out size)))
                    size = 0; // When the value is unavailable, GetSize fails; consider it 0

                argOrLocal.Size = size;

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
                StringBuilder typeName = new StringBuilder(MaxNameSize);
                uint typeNameLen;
                int hr;
                if (HR.Failed(hr = typeInstance.GetName(0 /*CLRDATA_GETNAME_DEFAULT*/, (uint)typeName.Capacity, out typeNameLen, typeName)))
                    return;

                // TODO For CLR v2 DAC on x64, if the return HRESULT is random garbage but
                // the name comes out OK, ignore the error (for now). This applies to multiple
                // methods that return names -- here, and IXCLRDataFrame::Get*ByIndex.

                argOrLocal.StaticTypeName = typeName.ToString();
                argOrLocal.ClrType = _context.Heap.GetTypeByName(argOrLocal.StaticTypeName);

                // TODO If the type is an array type (e.g. System.Byte[]), or a pointer type
                // (e.g. System.Byte*), or a by-ref type (e.g. System.Byte&), ClrHeap.GetTypeByName
                // will never return a good value. Think if we want to parse the name and "understand"
                // its kind and then look at the associated type and do a reconstruction, or
                // maybe IXCLRDataTypeInstance has some magic way of telling us what kind of type
                // it is (IXCLRDataTypeInstance::GetFlags always returns 0). This is only a
                // problem with variables that are either ref types and null (and then we can't
                // get the type name from the object itself), variables that are pointers, and
                // variables that are by-ref types.
                // Some starting points:
                //      - IXCLRDataValue::GetFlags (doesn't always return 0 :-))
                //      - IXCLRDataValue::GetAssociatedType returns the array element type/pointed to type
                //      - IXCLRDataValue::GetAssociatedValue returns the pointed to value

                // If the type is an inner type, IXCLRDataTypeInstance::GetName reports only
                // the inner part of the type. This isn't enough for ClrHeap.GetTypeByName,
                // so we have yet another option in that case -- searching by metadata token.
                if (argOrLocal.ClrType == null) 
                {
                    TryGetTypeByMetadataToken(argOrLocal, typeInstance);
                }

                // If the value is unavailable, we're done here.
                if (size == 0)
                    return;

                argOrLocal.Value = new byte[size];
                uint dataSize;
                if (HR.Failed(value.GetBytes((uint)argOrLocal.Value.Length, out dataSize, argOrLocal.Value)))
                    argOrLocal.Value = null;

                if (probablyReferenceType || argOrLocal.IsReferenceType)
                {
                    argOrLocal.ObjectAddress = Environment.Is64BitProcess ?
                        BitConverter.ToUInt64(argOrLocal.Value, 0) :
                        BitConverter.ToUInt32(argOrLocal.Value, 0);
                    // The type assigned here can be different from the previous value,
                    // because the static and dynamic type of the argument could differ.
                    // If the object reference is null or invalid, it could also be null --
                    // so we keep the previous type if it was already available.
                    argOrLocal.ClrType =
                        _context.Heap.GetObjectType(argOrLocal.ObjectAddress) ?? argOrLocal.ClrType;
                }

                FillLocation(argOrLocal, value);
            }

            private void FillLocation(ArgumentOrLocal argOrLocal, IXCLRDataValue value)
            {
                uint numLocs;
                if (HR.Failed(value.GetNumLocations(out numLocs)))
                    return;

                // Values could span multiple locations when they are enregistered. 
                // The IXCLRDataValue::GetBytes method is supposed to take care of it,
                // but we don't have a location to display. We don't even get the 
                // register name(s), so there's really nothing to display in this case.
                if (numLocs != 1)
                    return;

                uint flags;
                ulong address;
                if (HR.Failed(value.GetLocationByIndex(0, out flags, out address)))
                    return;

                // Only memory locations have a memory address.
                if (flags == (uint)ClrDataValueLocationFlag.CLRDATA_VLOC_MEMORY)
                {
                    argOrLocal.Location = address;
                }
            }

            private void TryGetTypeByMetadataToken(ArgumentOrLocal argOrLocal, IXCLRDataTypeInstance typeInstance)
            {
                object tmp;
                if (typeInstance.GetDefinition(out tmp) != HR.S_OK)
                    return;

                IXCLRDataTypeDefinition typeDefinition = (IXCLRDataTypeDefinition)tmp;
                int typeTok;
                if (HR.S_OK == typeDefinition.GetTokenAndScope(out typeTok, out tmp))
                {
                    IXCLRDataModule module = (IXCLRDataModule)tmp;
                    argOrLocal.ClrType = _context.GetTypeByMetadataToken(
                        GetModuleName(module), typeTok);
                    // This might fail if we don't have that type cached (unlikely)
                    // or if the type is generic, which makes the token non-unique.
                }
            }

            private string GetModuleName(IXCLRDataModule module)
            {
                StringBuilder name = new StringBuilder(MaxNameSize);
                uint nameLen;
                if (HR.Failed(module.GetName((uint)name.Capacity, out nameLen, name)))
                    return null;

                return name.ToString();
            }
        }
    }
}
