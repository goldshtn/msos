using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Runtime;
using System.Runtime.InteropServices;

using static msos.NativeStructs;
using static msos.NativeMethods;

namespace msos
{
    /// <summary>
    /// This class is responsible for fetching function parameters.
    /// it relies on x86 Calling Convention - passing parameters to function on the stack. 
    /// </summary>
    class StackWalkerStrategy_x86 : StackWalkerStrategy
    {
        public StackWalkerStrategy_x86(ClrRuntime runtime) : base(runtime)
        {
            if (_runtime.DataTarget.Architecture != Architecture.X86)
                throw new Exception("Unexpected architecture: only X86 is supported");
        }


        internal override UnifiedBlockingObject GetNtDelayExecutionBlockingObject(UnifiedStackFrame frame)
        {
            var parameters = GetParameters(frame, NTDELAY_EXECUTION_FUNCTION_PARAM_COUNT);
            var largeIntegerAddress = ConvertToAddress(parameters[1]);

            var largeInt = ReadStructureFromAddress<LARGE_INTEGER>(largeIntegerAddress);
            var awaitMs = (-largeInt.QuadPart) / 10000;

            return new UnifiedBlockingObject(awaitMs);
        }

        protected override UnifiedBlockingObject GetCriticalSectionBlockingObject(UnifiedStackFrame frame)
        {
            var parameters = GetParameters(frame, ENTER_CRITICAL_SECTION_FUNCTION_PARAM_COUNT);
            var criticalSectionAddress = ConvertToAddress(parameters[0]);

            var section = ReadStructureFromAddress<CRITICAL_SECTION>(criticalSectionAddress);
            return new UnifiedBlockingObject(section, criticalSectionAddress);
        }

        private T ReadStructureFromAddress<T>(ulong address)
        {
            T result = default(T);

            byte[] buffer = new byte[Marshal.SizeOf(typeof(T))];
            int read;

            if (!_runtime.ReadMemory(address, buffer, buffer.Length, out read) || read != buffer.Length)
                throw new Exception($"Error reading structure data from address: 0x{address.ToString("X")}");

            var gch = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                result = Marshal.PtrToStructure<T>(gch.AddrOfPinnedObject());
            }
            finally
            {
                gch.Free();
            }

            return result;
        }

        private List<byte[]> GetParameters(UnifiedStackFrame stackFrame, int paramCount)
        {
            List<byte[]> result = new List<byte[]>();
            var offset = stackFrame.FramePointer + 4; // Parameters start at EBP + 4
            int bytesRead = 0;

            for (int i = 0; i < paramCount; i++)
            {
                byte[] paramBuffer = new byte[IntPtr.Size];
                offset += (uint)IntPtr.Size;
                if (_runtime.ReadMemory(offset, paramBuffer, paramBuffer.Length, out bytesRead))
                {
                    result.Add(paramBuffer);
                }
            }

            return result;
        }

        protected override void ExtractWaitForMultipleObjectsInformation(UnifiedStackFrame frame)
        {
            var parameters = GetParameters(frame, WAIT_FOR_MULTIPLE_OBJECTS_PARAM_COUNT);
            if (parameters.Count > 0)
            {
                var numberOfHandles = BitConverter.ToUInt32(parameters[0], 0);
                var addressOfHandlesArray = ConvertToAddress(parameters[1]);
                AddMultipleWaitInformation(frame, numberOfHandles, addressOfHandlesArray);
            }
        }

        protected override void ExtractWaitForSingleObjectInformation(UnifiedStackFrame frame)
        {
            var parameters = GetParameters(frame, WAIT_FOR_SINGLE_OBJECT_PARAM_COUNT);
            if (parameters.Count > 0)
            {
                var handle = ConvertToAddress(parameters[0]);
                AddSingleWaitInformation(frame, handle);
            }
        }
    }

    /// <summary>
    /// Stack walker abstract class, defining stack walker interface
    /// </summary>
    abstract class StackWalkerStrategy
    {
        #region Constants

        public const string WAIT_FOR_SINGLE_OBJECT_FUNCTION_NAME = "WaitForSingleObject";
        public const string WAIT_FOR_SINGLE_OBJECT_EX_FUNCTION_NAME = "WaitForSingleObjectEx";
        public const string WAIT_FOR_MULTIPLE_OBJECTS_FUNCTION_NAME = "WaitForMultipleObjects";
        public const string WAIT_FOR_MULTIPLE_OBJECTS_EX_FUNCTION_NAME = "WaitForMultipleObjectsEx";
        public const string ENTER_CRITICAL_SECTION_FUNCTION_NAME = "RtlEnterCriticalSection";
        public const string NTDELAY_EXECUTION_FUNCTION_NAME = "NtDelayExecution";
        
        protected const int NTDELAY_EXECUTION_FUNCTION_PARAM_COUNT = 2;
        protected const int ENTER_CRITICAL_SECTION_FUNCTION_PARAM_COUNT = 1;
        protected const int WAIT_FOR_SINGLE_OBJECT_PARAM_COUNT = 2;
        protected const int WAIT_FOR_MULTIPLE_OBJECTS_PARAM_COUNT = 4;

        private const int MAXIMUM_WAIT_OBJECTS = 0x00000102;

        #endregion

        protected readonly ClrRuntime _runtime;

        public StackWalkerStrategy(ClrRuntime runtime)
        {
            _runtime = runtime;
        }

        private List<byte[]> ReadHandles(ulong startAddress, uint count)
        {
            List<byte[]> result = new List<byte[]>();
            for (uint i = 0; i < count; i++)
            {
                byte[] buffer = new byte[IntPtr.Size];
                int bytesRead;
                if (_runtime.ReadMemory(startAddress, buffer, IntPtr.Size, out bytesRead) &&
                    bytesRead == IntPtr.Size)
                {
                    result.Add(buffer);
                }
                else
                {
                    throw new Exception($"Error reading memory at {startAddress}");
                }

                startAddress += (ulong)IntPtr.Size;
            }
            return result;
        }

        private bool IsMatchingMethod(UnifiedStackFrame frame, string key)
        {
            return frame?.Method == key;
        }

        internal bool GetThreadSleepBlockingObject(UnifiedStackFrame frame, out UnifiedBlockingObject blockingObject)
        {
            blockingObject = null;

            if (IsMatchingMethod(frame, NTDELAY_EXECUTION_FUNCTION_NAME))
                blockingObject = GetNtDelayExecutionBlockingObject(frame);

            return blockingObject != null;
        }


        public bool GetCriticalSectionBlockingObject(UnifiedStackFrame frame, out UnifiedBlockingObject blockingObject)
        {
            blockingObject = null;

            if (frame.Handles != null && IsMatchingMethod(frame, ENTER_CRITICAL_SECTION_FUNCTION_NAME))
                blockingObject = GetCriticalSectionBlockingObject(frame);
           
            return blockingObject != null;
        }

        public bool SetFrameParameters(UnifiedStackFrame frame)
        {
            if (IsMatchingMethod(frame, WAIT_FOR_SINGLE_OBJECT_FUNCTION_NAME) ||
                IsMatchingMethod(frame, WAIT_FOR_SINGLE_OBJECT_EX_FUNCTION_NAME))
            {
                ExtractWaitForSingleObjectInformation(frame);
                return true;
            }

            if (IsMatchingMethod(frame, WAIT_FOR_MULTIPLE_OBJECTS_FUNCTION_NAME) ||
                IsMatchingMethod(frame, WAIT_FOR_MULTIPLE_OBJECTS_EX_FUNCTION_NAME))
            {
                ExtractWaitForMultipleObjectsInformation(frame);
                return true;
            }

            return false;
        }

        protected void AddSingleWaitInformation(UnifiedStackFrame frame, ulong handleValue)
        {
            // TODO The process id can't be zero, or this will always fail!
            //      Currently we are running the stack walker only for dump files, which means
            //      we don't have an id to run DuplicateHandle against.
            var typeName = GetHandleType((IntPtr)handleValue, 0);
            var objectName = GetHandleObjectName((IntPtr)handleValue, 0);

            UnifiedHandle unifiedHandle = new UnifiedHandle(handleValue, typeName, objectName);
            frame.Handles.Add(unifiedHandle);
        }

        protected void AddMultipleWaitInformation(UnifiedStackFrame frame, uint numberOfHandles, ulong addressOfHandlesArray)
        {
            if (numberOfHandles > MAXIMUM_WAIT_OBJECTS)
                numberOfHandles = MAXIMUM_WAIT_OBJECTS;

            var handles = ReadHandles(addressOfHandlesArray, numberOfHandles);
            foreach (var handleBytes in handles)
            {
                ulong handleValue = ConvertToAddress(handleBytes);
                AddSingleWaitInformation(frame, handleValue);
            }
        }

        protected static ulong ConvertToAddress(byte[] bits)
        {
            if (IntPtr.Size == 4)
            {
                return BitConverter.ToUInt32(bits, 0);
            }
            else
            {
                return BitConverter.ToUInt64(bits, 0);
            }
        }

        #region Abstract Methods

        protected abstract void ExtractWaitForSingleObjectInformation(UnifiedStackFrame frame);

        protected abstract void ExtractWaitForMultipleObjectsInformation(UnifiedStackFrame frame);

        internal abstract UnifiedBlockingObject GetNtDelayExecutionBlockingObject(UnifiedStackFrame frame);

        protected abstract UnifiedBlockingObject GetCriticalSectionBlockingObject(UnifiedStackFrame frame);

        #endregion

        #region NtQueryObject functions

        /// <summary>
        /// Retrieve the handle type name using NtQueryObject.
        /// </summary>
        private static string GetHandleType(IntPtr handle, uint pid)
        {
            IntPtr duplicatedHandle;
            if (!DuplicateHandle(handle, pid, out duplicatedHandle))
                return null;

            int length = Marshal.SizeOf(typeof(PUBLIC_OBJECT_TYPE_INFORMATION)), dummy;
            IntPtr buffer = Marshal.AllocHGlobal(length);
            try
            {
                NtStatus status = NtQueryObject(duplicatedHandle,
                    OBJECT_INFORMATION_CLASS.ObjectTypeInformation, buffer, length, out dummy);

                if (status == NtStatus.Success)
                {
                    var info = (PUBLIC_OBJECT_TYPE_INFORMATION)Marshal.PtrToStructure(buffer, typeof(PUBLIC_OBJECT_TYPE_INFORMATION));
                    return info.TypeName.ToString();
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                CloseHandle(duplicatedHandle);
            }

            return null;
        }

        /// <summary>
        /// Retrieves the name of the object referenced by the specified handle using NtQueryObject.
        /// </summary>
        private static string GetHandleObjectName(IntPtr handle, uint pid)
        {
            IntPtr duplicatedHandle;
            if (!DuplicateHandle(handle, pid, out duplicatedHandle))
                return null;

            int length = Marshal.SizeOf(typeof(OBJECT_NAME_INFORMATION)) + 256, dummy;
            IntPtr buffer = Marshal.AllocHGlobal(length);
            try
            {
                NtStatus status = NtQueryObject(duplicatedHandle,
                        OBJECT_INFORMATION_CLASS.ObjectNameInformation, buffer, length, out dummy);
                if (status == NtStatus.Success)
                {

                    OBJECT_NAME_INFORMATION info = (OBJECT_NAME_INFORMATION)Marshal.PtrToStructure(buffer, typeof(OBJECT_NAME_INFORMATION));
                    return info.Name.ToString();
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                CloseHandle(duplicatedHandle);
            }

            return null;
        }

        private static bool DuplicateHandle(IntPtr handle, uint pid, out IntPtr newHandle)
        {
            newHandle = IntPtr.Zero;

            var processHandle = OpenProcess(ProcessAccessFlags.DuplicateHandle, true, pid);

            var process = GetCurrentProcess();
            var options = DuplicateOptions.DUPLICATE_SAME_ACCESS;

            bool success = NativeMethods.DuplicateHandle(processHandle, handle, process, out newHandle, 0, false, options);
            CloseHandle(processHandle);

            return success;
        }

        #endregion

    }
}
