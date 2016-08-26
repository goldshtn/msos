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
            {
                throw new Exception("Unexpected Architecture");
            }
        }

        protected override UnifiedBlockingObject GetCriticalSectionBlockingObject(UnifiedStackFrame frame)
        {
            UnifiedBlockingObject result = null;
            var paramz = GetParams(frame, ENTER_CRITICAL_SECTION_FUNCTION_PARAM_COUNT);

            var address = Convert(paramz[0]);

            byte[] buffer = new byte[Marshal.SizeOf(typeof(CRITICAL_SECTION))];

            int read;

            if (!_runtime.ReadMemory(address, buffer, buffer.Length, out read) || read != buffer.Length)
                throw new Exception($"Address : {address}");

            var gch = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            try
            {
                CRITICAL_SECTION section = (CRITICAL_SECTION)Marshal.PtrToStructure
                    (gch.AddrOfPinnedObject(), typeof(CRITICAL_SECTION));

                result = new UnifiedBlockingObject(section, address);
            }
            finally
            {
                gch.Free();
            }
            return result;
        }

        List<byte[]> GetParams(UnifiedStackFrame stackFrame, int paramCount)
        {
            List<byte[]> result = new List<byte[]>();

            var offset = stackFrame.FrameOffset; //Base Pointer - % EBP
            byte[] paramBuffer;
            int bytesRead = 0;
            offset += 4;

            for (int i = 0; i < paramCount; i++)
            {
                paramBuffer = new byte[IntPtr.Size];
                offset += (uint)IntPtr.Size;
                if (_runtime.ReadMemory(offset, paramBuffer, 4, out bytesRead))
                {
                    result.Add(paramBuffer);
                }
            }

            return result;
        }

        protected override void DealWithCriticalSection(UnifiedStackFrame frame)
        {
            var paramz = GetParams(frame, ENTER_CRITICAL_SECTION_FUNCTION_PARAM_COUNT);
            var criticalSectionPtr = Convert(paramz[0]);
            frame.Handles = new List<UnifiedHandle>();
            frame.Handles.Add(new UnifiedHandle(criticalSectionPtr, UnifiedHandleType.CriticalSection));
        }

        protected override void DealWithMultiple(UnifiedStackFrame frame)
        {
            var paramz = GetParams(frame, WAIT_FOR_MULTIPLE_OBJECTS_PARAM_COUNT);
            if (paramz.Count > 0)
            {
                frame.Handles = new List<UnifiedHandle>();

                var HandlesCunt = BitConverter.ToUInt32(paramz[0], 0);
                var HandleAddress = BitConverter.ToUInt32(paramz[1], 0);

                EnrichUnifiedStackFrame(frame, HandlesCunt, HandleAddress);
            }
        }

        protected override void DealWithSingle(UnifiedStackFrame frame)
        {
            var paramz = GetParams(frame, WAIT_FOR_SINGLE_OBJECT_PARAM_COUNT);
            if (paramz.Count > 0)
            {
                var handle = Convert(paramz[0]);

                frame.Handles = new List<UnifiedHandle>();
                frame.Handles.Add(new UnifiedHandle(handle));
            }
        }
    }

    /// <summary>
    /// Stack walker abstract class, defining stack walker interface
    /// </summary>
    abstract class StackWalkerStrategy
    {
        #region Constants

        public const string WAIT_FOR_SINGLE_OBJECTS_FUNCTION_NAME = "WaitForSingleObject";
        public const string WAIT_FOR_MULTIPLE_OBJECTS_FUNCTION_NAME = "WaitForMultipleObjects";
        public const string ENTER_CRITICAL_SECTION_FUNCTION_NAME = "EnterCriticalSection";

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


        private List<byte[]> ReadFromMemory(ulong startAddress, uint count)
        {
            List<byte[]> result = new List<byte[]>();
            int sum = 0;
            for (int i = 0; i < count; i++)
            {
                byte[] readBytes = new byte[IntPtr.Size];
                if (_runtime.ReadMemory(startAddress, readBytes, IntPtr.Size, out sum))
                {
                    result.Add(readBytes);
                }
                else
                {
                    throw new Exception($"Accessing Unreadable memorry at {startAddress}");
                }

                startAddress += (ulong)IntPtr.Size;
            }
            return result;
        }

        internal bool CheckMethod(UnifiedStackFrame frame, string key)
        {
            bool result = frame != null
                && !String.IsNullOrEmpty(frame.Method)
                && frame.Method != null && frame.Method.Contains(key);

            return result;
        }

        internal bool GetCriticalSectionBlockingObject(UnifiedStackFrame frame, out UnifiedBlockingObject blockingObject)
        {
            bool result = false;

            if (frame.Handles != null && frame.Method.Contains(ENTER_CRITICAL_SECTION_FUNCTION_NAME))
            {
                blockingObject = GetCriticalSectionBlockingObject(frame);
                result = blockingObject != null;
            }
            else
            {
                blockingObject = null;
            }

            return result;
        }

        internal bool SetFrameParameters(UnifiedStackFrame frame, ClrRuntime runtime)
        {
            bool waitCallFound = false;

            if (waitCallFound = CheckMethod(frame, WAIT_FOR_SINGLE_OBJECTS_FUNCTION_NAME))
            {
                DealWithSingle(frame);
            }
            else if (waitCallFound = CheckMethod(frame, WAIT_FOR_MULTIPLE_OBJECTS_FUNCTION_NAME))
            {
                DealWithMultiple(frame);
            }
            else if (waitCallFound = CheckMethod(frame, ENTER_CRITICAL_SECTION_FUNCTION_NAME))
            {
                DealWithCriticalSection(frame);
            }

            return waitCallFound;
        }

        protected void EnrichUnifiedStackFrame(UnifiedStackFrame frame, ulong waitCount, ulong hPtr)
        {
            if (waitCount > MAXIMUM_WAIT_OBJECTS)
                waitCount = MAXIMUM_WAIT_OBJECTS;

            var handles = ReadFromMemory(hPtr, (uint)waitCount);

            frame.Handles = new List<UnifiedHandle>();
            foreach (var handle in handles)
            {
                ulong handleUint = Convert(handle);

                var typeName = GetHandleType((IntPtr)handleUint, 0);
                var handleName = GetHandleObjectName((IntPtr)handleUint, 0);

                UnifiedHandle unifiedHandle = new UnifiedHandle(handleUint, UnifiedHandleType.Handle, typeName, handleName);

                if (unifiedHandle != null)
                {
                    frame.Handles.Add(unifiedHandle);
                }
            }
        }

        protected ulong Convert(byte[] bits)
        {
            var integer = BitConverter.ToInt32(bits, 0);
            return (ulong)integer;
        }

        #region Abstract Methods

        protected abstract void DealWithSingle(UnifiedStackFrame frame);

        protected abstract void DealWithMultiple(UnifiedStackFrame frame);

        protected abstract void DealWithCriticalSection(UnifiedStackFrame frame);

        protected abstract UnifiedBlockingObject GetCriticalSectionBlockingObject(UnifiedStackFrame frame);

        #endregion

        #region NtQueryObject functions

        /// <summary>
        /// Gets Handle Type (String type name) using NtQueryObject NtDll function
        /// Doc: https://msdn.microsoft.com/en-us/library/bb432383(v=vs.85).aspx
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public static unsafe string GetHandleType(IntPtr handle, uint pid)
        {
            string result = null;

            IntPtr duplicatedHandle = IntPtr.Zero;

            if (Duplicate(handle, pid, out duplicatedHandle))
            {
                int length;

                NtStatus stat = NtQueryObject(duplicatedHandle,
                    OBJECT_INFORMATION_CLASS.ObjectTypeInformation, IntPtr.Zero, 0, out length);

                if (stat != NtStatus.InvalidHandle)
                {
                    IntPtr pointer = default(IntPtr);
                    try
                    {
                        pointer = Marshal.AllocHGlobal((int)length);

                        NtStatus status = NtQueryObject(duplicatedHandle,
                           OBJECT_INFORMATION_CLASS.ObjectTypeInformation, pointer, length, out length);

                        if (status == NtStatus.Success)
                        {
                            var info = (PUBLIC_OBJECT_TYPE_INFORMATION)Marshal.PtrToStructure(pointer, typeof(PUBLIC_OBJECT_TYPE_INFORMATION));
                            result = info.TypeName.ToString();
                        }
                    }
                    finally
                    {
                        if (pointer != default(IntPtr))
                        {
                            Marshal.FreeHGlobal(pointer);
                        }
                    }
                }

                CloseHandle(duplicatedHandle);
            }
            return result;
        }

        /// <summary>
        /// Gets Handle Object name using NtQueryObject NtDll function
        /// Doc: https://msdn.microsoft.com/en-us/library/bb432383(v=vs.85).aspx
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public static unsafe string GetHandleObjectName(IntPtr handle, uint pid)
        {
            string result = null;

            IntPtr duplicatedHandle = default(IntPtr);

            if (Duplicate(handle, pid, out duplicatedHandle))
            {
                int length;

                NtStatus stat = NtQueryObject(duplicatedHandle,
                    OBJECT_INFORMATION_CLASS.ObjectNameInformation, IntPtr.Zero, 0, out length);

                if (stat != NtStatus.InvalidHandle)
                {
                    IntPtr pointer = default(IntPtr);
                    try
                    {
                        pointer = Marshal.AllocHGlobal((int)length);

                        NtStatus status = NtQueryObject(duplicatedHandle,
                               OBJECT_INFORMATION_CLASS.ObjectNameInformation, pointer, length, out length);

                        if (status == NtStatus.Success)
                        {

                            OBJECT_NAME_INFORMATION info = (OBJECT_NAME_INFORMATION)Marshal.PtrToStructure(pointer, typeof(OBJECT_NAME_INFORMATION));
                            result = info.Name.ToString();
                        }
                    }
                    finally
                    {
                        if (pointer != default(IntPtr))
                        {
                            Marshal.FreeHGlobal(pointer);
                        }
                    }
                }

                CloseHandle(duplicatedHandle);
            }

            return result;
        }

        /// <summary>
        /// Perform handle duplication
        /// </summary>
        /// <param name="handle">Handle which needed to be duplicated</param>
        /// <param name="pid">Process PID (handle owner)</param>
        /// <param name="result">Duplicated Handle</param>
        /// <returns></returns>
        private static bool Duplicate(IntPtr handle, uint pid, out IntPtr result)
        {
            result = default(IntPtr);

            var processHandle = OpenProcess(ProcessAccessFlags.All, true, pid);

            var process = GetCurrentProcess();
            var options = DuplicateOptions.DUPLICATE_SAME_ACCESS;

            return DuplicateHandle(processHandle, handle, process, out result, 0, false, options);
        }

        #endregion

    }
}
