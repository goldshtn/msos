using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using static msos.NativeStructs;
using static msos.NativeMethods;

namespace msos
{
    /// <summary>
    /// This class extracts data from dump file uisng native windows api DbgHelp.h function. 
    /// </summary>
    class MiniDump
    {
        #region Members

        private SafeMemoryMappedViewHandle _safeMemoryMappedViewHandle;
        private IntPtr _baseOfView;

        #endregion

        public MiniDump(string dumpFileName)
        {
            Init(dumpFileName);
        }

        /// <summary>
        /// Initialization of SafeMemoryMappedViewHandle by the filePath 
        /// </summary>
        /// <param name="dumpFileName">full path to dump file</param>
        public void Init(string dumpFileName)
        {
            using (FileStream fileStream = File.Open(dumpFileName, System.IO.FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                _safeMemoryMappedViewHandle = MapFile(fileStream, dumpFileName);
                GetSystemInfo();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileStream"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static SafeMemoryMappedViewHandle MapFile(FileStream fileStream, string fileName)
        {
            MemoryMappedFile mappedFile = MemoryMappedFile.CreateFromFile(fileStream, Path.GetFileName(fileName), 0, MemoryMappedFileAccess.Read, null, HandleInheritability.None, false);

            SafeMemoryMappedViewHandle mappedFileView = MapViewOfFile(mappedFile.SafeMemoryMappedFileHandle, FileMapAccess.FileMapRead, 0, 0, IntPtr.Zero);

            MEMORY_BASIC_INFORMATION memoryInfo = default(MEMORY_BASIC_INFORMATION);

            if (VirtualQuery(mappedFileView, ref memoryInfo, (IntPtr)Marshal.SizeOf(memoryInfo)) == IntPtr.Zero)
            {
                Debug.WriteLine($"error:  {Marshal.GetLastWin32Error()}");
            }

            if (mappedFileView.IsInvalid)
            {
                Debug.WriteLine($"MapViewOfFile IsInvalid, error:  {Marshal.GetLastWin32Error()}");
            }

            mappedFileView.Initialize((ulong)memoryInfo.RegionSize);


            return mappedFileView;
        }


        #region MiniDump Handles

        /// <summary>
        /// Reads handles informations from previously inited SafeMemoryMappedViewHandle
        /// </summary>
        /// <returns>List of handles</returns>
        public List<MiniDumpHandle> GetHandles()
        {
            List<MiniDumpHandle> result = new List<MiniDumpHandle>();

            MINIDUMP_HANDLE_DATA_STREAM handleData;
            IntPtr streamPointer;
            uint streamSize;

            var readStream = SafeMemoryMappedViewStreamHandler.ReadStream(MINIDUMP_STREAM_TYPE.HandleDataStream, out handleData, out streamPointer, out streamSize, _safeMemoryMappedViewHandle, out _baseOfView);

            if (!readStream)
            {
                Debug.WriteLine($"Can't read stream ! ");
            }

            //Advancing the pointer
            streamPointer = streamPointer + (int)handleData.SizeOfHeader;

            if (handleData.SizeOfDescriptor == Marshal.SizeOf(typeof(MINIDUMP_HANDLE_DESCRIPTOR)))
            {
                MINIDUMP_HANDLE_DESCRIPTOR[] handles = SafeMemoryMappedViewStreamHandler.ReadArray<MINIDUMP_HANDLE_DESCRIPTOR>(streamPointer, (int)handleData.NumberOfDescriptors, _safeMemoryMappedViewHandle);

                foreach (var handle in handles)
                {
                    result.Add(new MiniDumpHandle(handle));
                }
            }
            else if (handleData.SizeOfDescriptor == Marshal.SizeOf(typeof(MINIDUMP_HANDLE_DESCRIPTOR_2)))
            {
                MINIDUMP_HANDLE_DESCRIPTOR_2[] handles = SafeMemoryMappedViewStreamHandler.ReadArray<MINIDUMP_HANDLE_DESCRIPTOR_2>(streamPointer, (int)handleData.NumberOfDescriptors, _safeMemoryMappedViewHandle);

                foreach (var handle in handles)
                {
                    MiniDumpHandle temp = GetHandleData(handle, streamPointer);

                    result.Add(temp);
                }
            }


            return result;
        }

        /// <summary>
        /// Constructs handles wrapped class with MINIDUMP_HANDLE_DESCRIPTOR_2 struct data
        /// </summary>
        /// <param name="handle">minidump struct descriptor</param>
        /// <param name="streamPointer">stream pointer</param>
        /// <returns></returns>
        private MiniDumpHandle GetHandleData(MINIDUMP_HANDLE_DESCRIPTOR_2 handle, IntPtr streamPointer)
        {
            string objectName, typeName;
            typeName = objectName = null;
            if (handle.ObjectNameRva != 0)
            {
                objectName = GetMiniDumpString(handle.ObjectNameRva, streamPointer);
            }
            if (handle.TypeNameRva != 0)
            {
                typeName = GetMiniDumpString(handle.TypeNameRva, streamPointer);
            }

            var result = new MiniDumpHandle(handle, objectName, typeName);
            objectName = GetMiniDumpString(handle.ObjectNameRva, streamPointer);
            if (handle.HandleCount > 0)
            {
                if (handle.ObjectInfoRva > 0)
                {
                    var info = SafeMemoryMappedViewStreamHandler.ReadStruct<MINIDUMP_HANDLE_OBJECT_INFORMATION>(handle.ObjectInfoRva, streamPointer, _safeMemoryMappedViewHandle);
                    if (info.NextInfoRva != 0)
                    {
                        IntPtr address = IntPtr.Add(_baseOfView, handle.ObjectInfoRva);


                        var pObjectInfo = (MINIDUMP_HANDLE_OBJECT_INFORMATION)Marshal.PtrToStructure(address, typeof(MINIDUMP_HANDLE_OBJECT_INFORMATION));


                        do
                        {
                            pObjectInfo = GetHandleInfo(pObjectInfo, result, address, _baseOfView);

                            if (pObjectInfo.NextInfoRva == 0)
                                break;
                        }
                        while (pObjectInfo.NextInfoRva != 0 && pObjectInfo.SizeOfInfo != 0);
                    }

                }

            }

            if (handle.PointerCount > 0)
            {
                //TODO: The meaning of this member depends on the handle type and the operating system.
                //This is the number kernel references to the object that this handle refers to. 
            }

            if (handle.GrantedAccess > 0)
            {
                //TODO: The meaning of this member depends on the handle type and the operating system.
            }

            if (handle.Attributes > 0)
            {
                //TODO: 
                //The attributes for the handle, this corresponds to OBJ_INHERIT, OBJ_CASE_INSENSITIVE, etc. 
            }

            return result;
        }

        #endregion

        /// <summary>
        /// Extracts System Info from the mapped dump file
        /// </summary>
        /// <returns>System Info</returns>
        public MiniDumpSystemInfo GetSystemInfo()
        {
            MiniDumpSystemInfo result = null;
            MINIDUMP_SYSTEM_INFO systemInfo;
            IntPtr streamPointer;
            uint streamSize;

            bool readResult = SafeMemoryMappedViewStreamHandler.ReadStream<MINIDUMP_SYSTEM_INFO>(MINIDUMP_STREAM_TYPE.SystemInfoStream, out systemInfo, out streamPointer, out streamSize, _safeMemoryMappedViewHandle);

            if (readResult)
            {
                result = new MiniDumpSystemInfo(systemInfo);
            }

            return result;
        }

        private string GetMiniDumpString(int rva, IntPtr streamPointer)
        {
            string result = String.Empty;
            try
            {
                var typeNameMinidumpString = SafeMemoryMappedViewStreamHandler.ReadStruct<MINIDUMP_STRING>(rva, streamPointer, _safeMemoryMappedViewHandle);
                result = SafeMemoryMappedViewStreamHandler.ReadString(rva, typeNameMinidumpString.Length, _safeMemoryMappedViewHandle);
            }
            catch (Exception ex)
            {

            }

            return result;
        }

        #region Object Information 

        /// <summary>
        /// Reads  sctructure form given handle if possible
        /// </summary>
        /// <param name="pObjectInfo">object info struct</param>
        /// <param name="handle">context handle</param>
        /// <param name="address">calculate rva address</param>
        /// <param name="baseOfView">base of mapped minidump file</param>
        /// <returns>Information structure or default value if no info detected</returns>
        public MINIDUMP_HANDLE_OBJECT_INFORMATION GetHandleInfo(MINIDUMP_HANDLE_OBJECT_INFORMATION pObjectInfo, MiniDumpHandle handle, IntPtr address, IntPtr baseOfView)
        {
            switch (pObjectInfo.InfoType)
            {
                case MINIDUMP_HANDLE_OBJECT_INFORMATION_TYPE.MiniHandleObjectInformationNone:
                    SetMiniHandleObjectInformationNone(handle, address); break;
                case MINIDUMP_HANDLE_OBJECT_INFORMATION_TYPE.MiniThreadInformation1:
                    SetMiniThreadInformation1(handle, address); break;
                case MINIDUMP_HANDLE_OBJECT_INFORMATION_TYPE.MiniMutantInformation1:
                    SetMiniMutantInformation1(handle, address); break;
                case MINIDUMP_HANDLE_OBJECT_INFORMATION_TYPE.MiniMutantInformation2:
                    SetMiniMutantInformation2(handle, address); break;
                case MINIDUMP_HANDLE_OBJECT_INFORMATION_TYPE.MiniProcessInformation1:
                    SetMiniProcessInformation1(handle, address); break;
                case MINIDUMP_HANDLE_OBJECT_INFORMATION_TYPE.MiniProcessInformation2:
                    SetMiniProcessInformation2(handle, address); break;
                case MINIDUMP_HANDLE_OBJECT_INFORMATION_TYPE.MiniEventInformation1:
                    SetMiniEventInformation1(handle, address); break;
                case MINIDUMP_HANDLE_OBJECT_INFORMATION_TYPE.MiniSectionInformation1:
                    SetMiniSectionInformation1(handle, address); break;
                case MINIDUMP_HANDLE_OBJECT_INFORMATION_TYPE.MiniHandleObjectInformationTypeMax: SetMiniHandleObjectInformationTypeMax(handle, address); break;
                default:
                    break;
            }

            if (pObjectInfo.NextInfoRva == 0)
            {
                return default(MINIDUMP_HANDLE_OBJECT_INFORMATION);
            }
            else
            {
                var ptr = IntPtr.Add(baseOfView, (int)pObjectInfo.NextInfoRva);
                pObjectInfo = (MINIDUMP_HANDLE_OBJECT_INFORMATION)Marshal.PtrToStructure(ptr, typeof(MINIDUMP_HANDLE_OBJECT_INFORMATION));
            }

            return pObjectInfo;
        }

        #region Actions

        void SetMiniHandleObjectInformationTypeMax(MiniDumpHandle handle, IntPtr address)
        {
            handle.Type = MiniDumpHandleType.TYPE_MAX;
        }

        void SetMiniSectionInformation1(MiniDumpHandle handle, IntPtr address)
        {
            handle.Type = MiniDumpHandleType.SECTION;
        }

        void SetMiniEventInformation1(MiniDumpHandle handle, IntPtr address)
        {
            handle.Type = MiniDumpHandleType.EVENT;
        }

        void SetMiniHandleObjectInformationNone(MiniDumpHandle handle, IntPtr address)
        {
            handle.Type = MiniDumpHandleType.NONE;
        }

        void SetMiniProcessInformation2(MiniDumpHandle handle, IntPtr address)
        {
            handle.Type = MiniDumpHandleType.PROCESS2;


            var additional_info = (PROCESS_ADDITIONAL_INFO_2)Marshal.PtrToStructure(address, typeof(PROCESS_ADDITIONAL_INFO_2));
            handle.OwnerProcessId = (uint)additional_info.ProcessId;
            handle.OwnerThreadId = 0;
        }

        void SetMiniProcessInformation1(MiniDumpHandle handle, IntPtr address)
        {
            handle.Type = MiniDumpHandleType.PROCESS1;
        }

        void SetMiniMutantInformation2(MiniDumpHandle handle, IntPtr address)
        {
            handle.Type = MiniDumpHandleType.MUTEX2;


            var additional_info = (MUTEX_ADDITIONAL_INFO_2)Marshal.PtrToStructure(address, typeof(MUTEX_ADDITIONAL_INFO_2));

            handle.OwnerProcessId = (uint)additional_info.OwnerProcessId;
            handle.OwnerThreadId = (uint)additional_info.OwnerThreadId;
        }

        void SetMiniMutantInformation1(MiniDumpHandle handle, IntPtr address)
        {
            handle.Type = MiniDumpHandleType.MUTEX1;

            var additional_info = (MUTEX_ADDITIONAL_INFO_1)Marshal.PtrToStructure(address, typeof(MUTEX_ADDITIONAL_INFO_1));

            handle.MutexUnknown = new MutexUnknownFields()
            {
                Field1 = additional_info.Unknown1,
                Field2 = additional_info.Unknown2
            };
        }

        void SetMiniThreadInformation1(MiniDumpHandle handle, IntPtr address)
        {
            handle.Type = MiniDumpHandleType.THREAD;

            var additional_info = (THREAD_ADDITIONAL_INFO)Marshal.PtrToStructure(address, typeof(THREAD_ADDITIONAL_INFO));

            handle.OwnerProcessId = (uint)additional_info.ProcessId;
            handle.OwnerThreadId = (uint)additional_info.ThreadId;
        }

        #endregion

        #endregion
    }

    class MiniDumpSystemInfo
    {
        private MINIDUMP_SYSTEM_INFO _systemInfo;
        public X86CpuInfo X86CpuInfo { get; private set; }
        public NonX86CpuInfo OtherCpuInfo { get; private set; }
        public bool IsX86 { get; set; }

        internal MiniDumpSystemInfo(MINIDUMP_SYSTEM_INFO systemInfo)
        {
            _systemInfo = systemInfo;

            IsX86 = this.ProcessorArchitecture == MiniDumpProcessorArchitecture.PROCESSOR_ARCHITECTURE_INTEL;

            if (IsX86)
            {
                X86CpuInfo = new X86CpuInfo(_systemInfo.Cpu);
            }
            else
            {
                OtherCpuInfo = new NonX86CpuInfo(_systemInfo.Cpu);
            }
        }

        public MiniDumpProcessorArchitecture ProcessorArchitecture { get { return (MiniDumpProcessorArchitecture)_systemInfo.ProcessorArchitecture; } }

        public ushort ProcessorLevel { get { return _systemInfo.ProcessorLevel; } }

        public ushort ProcessorRevision { get { return _systemInfo.ProcessorRevision; } }

        public MiniDumpProductType ProductType { get { return (MiniDumpProductType)_systemInfo.ProductType; } }

        public uint MajorVersion { get { return _systemInfo.MajorVersion; } }

        public uint MinorVersion { get { return _systemInfo.MinorVersion; } }

        public uint BuildNumber { get { return _systemInfo.BuildNumber; } }

        public MiniDumpPlatform PlatformId { get { return (MiniDumpPlatform)_systemInfo.PlatformId; } }

    }

    class NonX86CpuInfo
    {
        private CPU_INFORMATION _cpuInfo;
        private ulong[] _processorFeatures;

        internal unsafe NonX86CpuInfo(CPU_INFORMATION cpuInfo)
        {
            _cpuInfo = cpuInfo;

            _processorFeatures = new ulong[2];
            _processorFeatures[0] = cpuInfo.ProcessorFeatures[0];
            _processorFeatures[1] = cpuInfo.ProcessorFeatures[1];
        }

        public ulong[] ProcessorFeatures { get { return this._processorFeatures; } }
    }

    class X86CpuInfo
    {
        private CPU_INFORMATION _cpuInfo;
        private uint[] _vendorIdRaw;
        private string _vendorId;

        internal unsafe X86CpuInfo(CPU_INFORMATION cpuInfo)
        {
            _cpuInfo = cpuInfo;

            _vendorIdRaw = new uint[3];
            _vendorIdRaw[0] = cpuInfo.VendorId[0];
            _vendorIdRaw[1] = cpuInfo.VendorId[1];
            _vendorIdRaw[2] = cpuInfo.VendorId[2];

            char[] vendorId = new char[12];

            GCHandle handle = GCHandle.Alloc(vendorId, GCHandleType.Pinned);

            try
            {
                ASCIIEncoding.ASCII.GetChars(cpuInfo.VendorId, 12, (char*)handle.AddrOfPinnedObject(), 12);
                _vendorId = new String(vendorId);
            }
            finally
            {
                handle.Free();
            }
        }

        public uint[] VendorIdRaw { get { return _vendorIdRaw; } }
        public string VendorId { get { return this._vendorId; } }
        public uint VersionInformation { get { return _cpuInfo.VersionInformation; } }
        public uint FeatureInformation { get { return _cpuInfo.FeatureInformation; } }
        public uint AMDExtendedCpuFeatures { get { return _cpuInfo.AMDExtendedCpuFeatures; } }
    }


    class SafeMemoryMappedViewStreamHandler
    {
        public static unsafe bool ReadStream<T>(MINIDUMP_STREAM_TYPE streamType, out T streamData, out IntPtr streamPointer, out uint streamSize, SafeMemoryMappedViewHandle safeMemoryMappedViewHandle)
        {
            IntPtr viewBase;
            return ReadStream<T>(streamType, out streamData, out streamPointer, out streamSize, safeMemoryMappedViewHandle, out viewBase);
        }

        public static unsafe bool ReadStream<T>(MINIDUMP_STREAM_TYPE streamType, out T streamData, out IntPtr streamPointer, out uint streamSize, SafeMemoryMappedViewHandle safeMemoryMappedViewHandle, out IntPtr viewBase)
        {
            bool result = false;
            MINIDUMP_DIRECTORY directory = new MINIDUMP_DIRECTORY();
            streamData = default(T);
            streamPointer = IntPtr.Zero;
            streamSize = 0;

            try
            {
                byte* baseOfView = null;
                safeMemoryMappedViewHandle.AcquirePointer(ref baseOfView);

                result = MiniDumpReadDumpStream((IntPtr)baseOfView, streamType, ref directory, ref streamPointer, ref streamSize);
                viewBase = (IntPtr)baseOfView;
                if (result)
                {
                    streamData = (T)Marshal.PtrToStructure(streamPointer, typeof(T));
                }
            }
            finally
            {
                safeMemoryMappedViewHandle.ReleasePointer();
            }

            return result;
        }

        public static string ReadString(int rva, uint length, SafeMemoryMappedViewHandle safeHandle)
        {
            return RunSafe<string>(() =>
            {
                unsafe
                {
                    byte* baseOfView = null;
                    safeHandle.AcquirePointer(ref baseOfView);
                    IntPtr positionToReadFrom = new IntPtr(baseOfView + rva);
                    positionToReadFrom += (int)length;

                    return Marshal.PtrToStringUni(positionToReadFrom);
                }
            }, safeHandle);
        }

        public static unsafe T[] ReadArray<T>(IntPtr absoluteAddress,
            int count, SafeMemoryMappedViewHandle safeHandle) where T : struct
        {
            return RunSafe<T>(() =>
            {
                T[] readItems = new T[count];

                byte* baseOfView = null;
                safeHandle.AcquirePointer(ref baseOfView);
                ulong offset = (ulong)absoluteAddress - (ulong)baseOfView;
                safeHandle.ReadArray<T>(offset, readItems, 0, count);
                return readItems;

            }, safeHandle, count);
        }

        public static T ReadStruct<T>(Int32 rva, IntPtr streamPtr, SafeMemoryMappedViewHandle safeHandle) where T : struct
        {
            return RunSafe<T>(() =>
            {
                unsafe
                {
                    byte* baseOfView = null;
                    safeHandle.AcquirePointer(ref baseOfView);
                    ulong offset = (ulong)streamPtr - (ulong)baseOfView;
                    return safeHandle.Read<T>(offset);
                }

            }, safeHandle);
        }

        public static T RunSafe<T>(Func<T> func, SafeMemoryMappedViewHandle safeHandle)
        {
            T result = default(T);
            try
            {
                result = func();
            }
            finally
            {
                safeHandle.ReleasePointer();
            }
            return result;
        }

        public static T[] RunSafe<T>(Func<T[]> function, SafeMemoryMappedViewHandle safeHandle, int count) where T : struct
        {
            T[] result = new T[count];
            try
            {
                result = function();
            }
            finally
            {
                safeHandle.ReleasePointer();
            }
            return result;
        }
    }


    enum MiniDumpHandleType
    {
        NONE,
        THREAD,
        MUTEX1,
        MUTEX2,
        PROCESS1,
        PROCESS2,
        EVENT,
        SECTION,
        TYPE_MAX
    }

    class MiniDumpHandle
    {

        public MiniDumpHandle(MINIDUMP_HANDLE_DESCRIPTOR handleDescriptor)
        {
            Handle = handleDescriptor.Handle;
            HandleCount = handleDescriptor.HandleCount;
            ObjectNameRva = handleDescriptor.ObjectNameRva;
            PointerCount = handleDescriptor.PointerCount;
            TypeNameRva = handleDescriptor.TypeNameRva;
            Attributes = handleDescriptor.Attributes;
            GrantedAccess = handleDescriptor.GrantedAccess;
        }

        public MiniDumpHandle(MINIDUMP_HANDLE_DESCRIPTOR_2 handleDescriptor)
        {
            Handle = handleDescriptor.Handle;
            HandleCount = handleDescriptor.HandleCount;
            ObjectNameRva = handleDescriptor.ObjectNameRva;
            PointerCount = handleDescriptor.PointerCount;
            TypeNameRva = handleDescriptor.TypeNameRva;
            Attributes = handleDescriptor.Attributes;
            GrantedAccess = handleDescriptor.GrantedAccess;
            ObjectInfoRva = handleDescriptor.ObjectInfoRva;
        }

        public MiniDumpHandle(MINIDUMP_HANDLE_DESCRIPTOR_2 handleDescriptor, string objectName, string typeName) : this(handleDescriptor)
        {
            ObjectName = objectName;
            TypeName = typeName;
        }

        public string ObjectName { get; private set; }
        public string TypeName { get; private set; }
        public ulong Handle { get; private set; }
        public uint HandleCount { get; private set; }

        /// <summary>
        /// An RVA to a MINIDUMP_STRING structure that specifies the object name of the handle. This member can be 0.
        /// </summary>
        public Int32 ObjectNameRva { get; private set; }
        public uint PointerCount { get; private set; }
        public Int32 TypeNameRva { get; private set; }
        public uint Attributes { get; private set; }
        public uint GrantedAccess { get; private set; }

        public MiniDumpHandleType Type { get; set; }
        /// <summary>
        /// An RVA to a MINIDUMP_HANDLE_OBJECT_INFORMATION structure that specifies object-specific information. This member can be 0 if there is no extra information.
        /// </summary>
        public Int32 ObjectInfoRva { get; private set; }
        public bool HasObjectInfo { get { return ObjectInfoRva > 0; } }

        internal MiniDumpHandleInfo HandleInfo { get; private set; }
        public uint OwnerProcessId { get; internal set; }
        public uint OwnerThreadId { get; internal set; }
        public MutexUnknownFields MutexUnknown { get; internal set; }
        public string Name { get; internal set; }
        public UnifiedBlockingReason UnifiedType { get { return GetAsUnified(Type); } }

        public UnifiedBlockingReason GetAsUnified(MiniDumpHandleType type)
        {
            UnifiedBlockingReason result = UnifiedBlockingReason.Unknown;

            switch (type)
            {
                case MiniDumpHandleType.NONE:
                    result = UnifiedBlockingReason.None;
                    break;
                case MiniDumpHandleType.THREAD:
                    result = UnifiedBlockingReason.Thread;
                    break;
                case MiniDumpHandleType.MUTEX1:
                    result = UnifiedBlockingReason.Mutex;
                    break;
                case MiniDumpHandleType.MUTEX2:
                    result = UnifiedBlockingReason.Mutex;
                    break;
                case MiniDumpHandleType.PROCESS1:
                    result = UnifiedBlockingReason.ProcessWait;
                    break;
                case MiniDumpHandleType.PROCESS2:
                    result = UnifiedBlockingReason.ProcessWait;
                    break;
                case MiniDumpHandleType.EVENT:
                    result = UnifiedBlockingReason.ThreadWait;
                    break;
                case MiniDumpHandleType.SECTION:
                    result = UnifiedBlockingReason.MemorySection;
                    break;
                case MiniDumpHandleType.TYPE_MAX:
                    break;
                default:
                    result = UnifiedBlockingReason.Unknown;
                    break;
            }
            return result;
        }

    }

    class MutexUnknownFields
    {
        public object Field1 { get; internal set; }
        public object Field2 { get; internal set; }
    }

    class MiniDumpHandleInfo
    {
        public MiniDumpHandleInfo(MINIDUMP_HANDLE_OBJECT_INFORMATION info)
        {
            this.NextInfoRva = info.NextInfoRva;
            this.SizeOfInfo = info.SizeOfInfo;
            this.InfoType = info.InfoType;
        }

        public uint NextInfoRva { get; private set; }
        public MINIDUMP_HANDLE_OBJECT_INFORMATION_TYPE InfoType { get; private set; }
        public UInt32 SizeOfInfo { get; private set; }
    }
}
