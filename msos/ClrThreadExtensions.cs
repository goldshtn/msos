using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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
                new FrameArgumentsAndLocalsRetriever(thread, stackTrace, context).ArgsAndLocals :
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
                    frameArgsAndLocals.Arguments.ForEach(a => DisplayOneArgumentOrLocal(context, "arg", a));
                    frameArgsAndLocals.LocalVariables.ForEach(l => DisplayOneArgumentOrLocal(context, "lcl", l));
                }
            }
        }

        private static void DisplayOneArgumentOrLocal(CommandExecutionContext context, string which, ArgumentOrLocal argOrLocal)
        {
            context.Write("  {0} {1,-10} = {2} ({3},{4} {5} bytes) ",
                which, argOrLocal.Name, argOrLocal.ValueRaw(), argOrLocal.DynamicTypeName,
                argOrLocal.StaticAndDynamicTypesAreTheSame ? "" : (" original " + argOrLocal.StaticTypeName + ","),
                argOrLocal.Size);
            if (argOrLocal.ObjectAddress != 0)
            {
                context.WriteLink("", String.Format("!do {0:x16}", argOrLocal.ObjectAddress));
            }
            else if (argOrLocal.HasNonTrivialValueToDisplay)
            {
                context.WriteLink("",
                    String.Format("!do {0:x16} --type {1}", argOrLocal.Location, argOrLocal.ClrType.Name));
            }
            context.WriteLine();
        }

        class ArgumentOrLocal
        {
            public string Name;
            public string StaticTypeName;
            public ulong Size;
            public byte[] Value;
            public ulong ObjectAddress; // If it's a reference type
            public ClrType ClrType;
            public ulong Location;      // The arg/local's address, not value

            public bool StaticAndDynamicTypesAreTheSame
            {
                get { return StaticTypeName == DynamicTypeName; }
            }

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
                    // NOTE ObjectAddress could be 0 while the value is simply unavailable.

                    // Display strings inline.
                    if (ClrType.IsString && Value != null)
                        return ClrType.GetValue(ObjectAddress).ToStringOrNull();

                    // Display objects (non-strings) as an address.
                    if (ClrType.IsObjectReference && Value != null)
                        return String.Format("{0:x16}", ObjectAddress);

                    if (HasNonTrivialValueToDisplay)
                        return "VALTYPE";

                    // Display primitive value types inline.
                    if (ClrType.HasSimpleValue && Location != 0)
                        return ClrType.GetPrimitiveValueNonBoxed(Location).ToStringOrNull();
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
            private IList<ClrStackFrame> _stackTrace;
            private bool _isOnCLRv2;

            public FrameArgumentsAndLocals[] ArgsAndLocals { get { return _results.ToArray(); } }

            public FrameArgumentsAndLocalsRetriever(ClrThread thread,
                IList<ClrStackFrame> stackTrace, CommandExecutionContext context)
            {
                _context = context;
                _stackTrace = stackTrace;

                _isOnCLRv2 = _context.ClrVersion.Version.Major == 2;

                ProcessStackWalk(thread.OSThreadId);
            }

            private void ProcessStackWalk(uint osThreadId)
            {
                IXCLRDataProcess ixclrDataProcess = _context.Runtime.DacInterface;

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

            private bool FailedUnlessOnCLR2DAC(int hr)
            {
                // For CLR v2 DAC, if the return HRESULT is random garbage, ignore the error.
                // This happens because of a source-level bug in the DAC that returns an 
                // uninitialized stack variable. This was confirmed by a Microsoft engineer.
                if (_isOnCLRv2)
                    return false;

                return HR.Failed(hr);
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

                    if (FailedUnlessOnCLR2DAC(frame.GetArgumentByIndex(argIdx, out tmp, (uint)argName.Capacity, out argNameLen, argName)) ||
                        tmp == null)
                        continue;

                    var arg = new ArgumentOrLocal() { Name = argName.ToString() };
                    FillValue(arg, (IXCLRDataValue)tmp);
                    frameArgsLocals.Arguments.Add(arg);
                }

                for (uint lclIdx = 0; lclIdx < numLocals; ++lclIdx)
                {
                    // The mscordacwks!ClrDataFrame::GetLocalVariableByIndex implementation never returns
                    // names for local variables. Need to go through metadata to get them.
                    StringBuilder dummy = new StringBuilder(2);
                    uint lclNameLen;
                    if (FailedUnlessOnCLR2DAC(frame.GetLocalVariableByIndex(lclIdx, out tmp, (uint)dummy.Capacity, out lclNameLen, dummy)) ||
                        tmp == null)
                        continue;

                    string lclName = dummy.ToString();
                    var matchingFrame = _stackTrace.SingleOrDefault(f => f.DisplayString == frameArgsLocals.MethodName);
                    if (matchingFrame != null)
                    {
                        lclName = GetLocalVariableName(matchingFrame.InstructionPointer, lclIdx);
                    }

                    var lcl = new ArgumentOrLocal() { Name = lclName };
                    FillValue(lcl, (IXCLRDataValue)tmp);
                    frameArgsLocals.LocalVariables.Add(lcl);
                }

                _results.Add(frameArgsLocals);
            }

            private string GetLocalVariableName(ulong instructionPointer, uint localIndex)
            {
                ClrMethod method = _context.Runtime.GetMethodByAddress(instructionPointer);
                ClrModule module = method.Type.Module;
                string pdbLocation = module.TryDownloadPdb(null);
                IntPtr iunkMetadataImport = Marshal.GetIUnknownForObject(module.MetadataImport);
                ISymbolReader reader = null;
                ISymbolMethod symMethod = null;
                try
                {
                    using (var binder = new SymBinder())
                    {
                        reader = binder.GetReader(
                            iunkMetadataImport, module.FileName, Path.GetDirectoryName(pdbLocation));

                        symMethod = reader.GetMethod(new SymbolToken((int)method.MetadataToken));
                        return GetLocalVariableName(symMethod.RootScope, localIndex);
                    }
                }
                catch (COMException comEx)
                {
                    // E_FAIL occasionally occurs in ISymbolReader.GetMethod. Nothing we can do about it.
                    if ((uint)comEx.HResult == 0x80004005)
                        return "";

                    // 0x806D0005 occurs when the PDB cannot be found or doesn't contain the necessary
                    // information to create a symbol reader. It's OK to ignore.
                    if ((uint)comEx.HResult == 0x806D0005)
                        return "";

                    throw;
                }
                finally
                {
                    // These interfaces aren't IDisposable, but the underlying objects are. And it is
                    // important to dispose of them properly, because if their finalizer runs on exit,
                    // it crashes with an access violation.
                    if (reader != null)
                        ((IDisposable)reader).Dispose();
                    if (symMethod != null)
                        ((IDisposable)symMethod).Dispose();

                    Marshal.Release(iunkMetadataImport);
                }
            }

            private string GetLocalVariableName(ISymbolScope scope, uint localIndex)
            {
                ISymbolVariable[] localVariables = null;
                try
                {
                    localVariables = scope.GetLocals();
                    foreach (var localVar in localVariables)
                    {
                        if (localVar.AddressKind == SymAddressKind.ILOffset &&
                            localVar.AddressField1 == localIndex)
                            return localVar.Name;
                    }

                    foreach (var childScope in scope.GetChildren())
                    {
                        string result = GetLocalVariableName(childScope, localIndex);
                        if (result != null)
                            return result;
                    }

                    return null;
                }
                finally
                {
                    if (localVariables != null)
                    {
                        foreach (var localVar in localVariables)
                            ((IDisposable)localVar).Dispose();
                    }
                    ((IDisposable)scope).Dispose();
                }
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
                if (FailedUnlessOnCLR2DAC(typeInstance.GetName(0 /*CLRDATA_GETNAME_DEFAULT*/, (uint)typeName.Capacity, out typeNameLen, typeName)))
                    return;

                argOrLocal.StaticTypeName = typeName.ToString();
                argOrLocal.ClrType = _context.Heap.GetTypeByName(argOrLocal.StaticTypeName);

                // If the value is unavailable, we're done here.
                if (size == 0)
                    return;

                FillLocation(argOrLocal, value);

                argOrLocal.Value = new byte[size];
                uint dataSize;
                if (HR.Failed(value.GetBytes((uint)argOrLocal.Value.Length, out dataSize, argOrLocal.Value)))
                    argOrLocal.Value = null;

                // If the type is an array type (e.g. System.Byte[]), or a pointer type
                // (e.g. System.Byte*), or a by-ref type (e.g. System.Byte&), ClrHeap.GetTypeByName
                // will never return a good value. This is only a problem with variables that
                // are either ref types and null (and then we can't get the type name from
                // the object itself), variables that are pointers, and variables that are
                // by-ref types. Here's the plan:
                //  1) If the variable is a ref type and is null, we don't care about the
                //     ClrType being correct anyway. We report the type returned by GetName
                //     above, and report the value as null.
                //  2) If the value is a by-ref type or a pointer type, IXCLRDataValue::GetFlags
                //     can detect it. Then, we keep the ClrType null (because ClrMD doesn't have
                //     a representation for pointer types or by-ref types), but we read the
                //     value anyway by dereferencing the pointer. According to the comments in
                //     xclrdata.idl, IXCLRDataValue::GetAssociatedValue is supposed to return the
                //     pointed-to value, but it doesn't (it only works for references).

                uint vf;
                CLRDataValueFlag valueFlags = CLRDataValueFlag.Invalid;
                if (HR.S_OK == value.GetFlags(out vf))
                    valueFlags = (CLRDataValueFlag)vf;
                
                // * Pointers are identified as CLRDATA_VALUE_IS_POINTER.
                // * By-refs are identified as CLRDATA_VALUE_DEFAULT regardless of referenced type.
                bool byRefOrPointerType =
                    (valueFlags & CLRDataValueFlag.CLRDATA_VALUE_IS_POINTER) != 0 ||
                    (valueFlags == CLRDataValueFlag.CLRDATA_VALUE_DEFAULT /* it is 0 */);
                if (byRefOrPointerType)
                {
                    // By-refs to pointers are identified as CLRDATA_VALUE_DEFAULT with target UInt64,
                    // which makes them undistinguishable from 'ref ulong', unfortunately. But if the 
                    // type name reported didn't include the &, we know that's what it is.
                    if (argOrLocal.StaticTypeName == "System.UInt64")
                    {
                        argOrLocal.ClrType = null; // We don't really know what the type is
                        argOrLocal.StaticTypeName = "UNKNOWN*&";
                    }

                    if (argOrLocal.Value != null)
                    {
                        ulong ptrValue = RawBytesToAddress(argOrLocal.Value);

                        ulong potentialReference;
                        if (_context.Runtime.ReadPointer(ptrValue, out potentialReference) &&
                            (argOrLocal.ClrType = _context.Heap.GetObjectType(potentialReference)) != null)
                        {
                            // If the type was resolved, then this was the address of a heap object.
                            // In that case, we're done and we have a type.
                            argOrLocal.ObjectAddress = potentialReference;
                        }
                        else
                        {
                            // Otherwise, this address is the address of a value type. We don't know
                            // which type, because IXCLRDataValue::GetAssociatedType doesn't return anything
                            // useful when the value is a pointer or by-ref. But we can try to remove
                            // the * or & from the type name, and then try to figure out what the target
                            // type is.
                            string noRefNoPtrTypeName = argOrLocal.StaticTypeName.TrimEnd('&', '*');
                            if ((argOrLocal.ClrType = _context.Heap.GetTypeByName(noRefNoPtrTypeName))
                                != null)
                            {                                
                                argOrLocal.Location = ptrValue;
                            }
                        }
                    }
                }
                
                if (argOrLocal.ClrType == null)
                {
                    // If the type is an inner type, IXCLRDataTypeInstance::GetName reports only
                    // the inner part of the type. This isn't enough for ClrHeap.GetTypeByName,
                    // so we have yet another option in that case -- searching by metadata token.
                    TryGetTypeByMetadataToken(argOrLocal, typeInstance);

                    // If we had a pointer or by-ref type and didn't know what it was, we now do,
                    // so we can store its location and have it displayed.
                    if (byRefOrPointerType && argOrLocal.ClrType != null)
                        argOrLocal.Location = RawBytesToAddress(argOrLocal.Value);
                }

                if (!byRefOrPointerType && (probablyReferenceType || argOrLocal.IsReferenceType))
                {
                    argOrLocal.ObjectAddress = RawBytesToAddress(argOrLocal.Value);
                    // The type assigned here can be different from the previous value,
                    // because the static and dynamic type of the argument could differ.
                    // If the object reference is null or invalid, it could also be null --
                    // so we keep the previous type if it was already available.
                    argOrLocal.ClrType =
                        _context.Heap.GetObjectType(argOrLocal.ObjectAddress) ?? argOrLocal.ClrType;
                }
            }

            private static ulong RawBytesToAddress(byte[] rawBytes)
            {
                return Environment.Is64BitProcess ?
                        BitConverter.ToUInt64(rawBytes, 0) :
                        BitConverter.ToUInt32(rawBytes, 0);
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
