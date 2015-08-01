using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("5c552ab6-fc09-4cb3-8e36-22fa03c798b7")]
    public interface IXCLRDataProcess
    {
        void Flush();

        void StartEnumTasks_do_not_use();
        void EnumTask_do_not_use();
        void EndEnumTasks_do_not_use();
        [PreserveSig]
        int GetTaskByOSThreadID(uint id, [Out, MarshalAs(UnmanagedType.IUnknown)] out object task);
        void GetTaskByUniqueID_do_not_use(/*[in] ULONG64 taskID, [out] IXCLRDataTask** task*/);
        void GetFlags_do_not_use(/*[out] ULONG32* flags*/);
        void IsSameObject_do_not_use(/*[in] IXCLRDataProcess* process*/);
        void GetManagedObject_do_not_use(/*[out] IXCLRDataValue** value*/);
        void GetDesiredExecutionState_do_not_use(/*[out] ULONG32* state*/);
        void SetDesiredExecutionState_do_not_use(/*[in] ULONG32 state*/);
        void GetAddressType_do_not_use(/*[in] CLRDATA_ADDRESS address, [out] CLRDataAddressType* type*/);
        void GetRuntimeNameByAddress_do_not_use(/*[in] CLRDATA_ADDRESS address, [in] ULONG32 flags, [in] ULONG32 bufLen, [out] ULONG32 *nameLen, [out, size_is(bufLen)] WCHAR nameBuf[], [out] CLRDATA_ADDRESS* displacement*/);
        void StartEnumAppDomains_do_not_use(/*[out] CLRDATA_ENUM* handle*/);
        void EnumAppDomain_do_not_use(/*[in, out] CLRDATA_ENUM* handle, [out] IXCLRDataAppDomain** appDomain*/);
        void EndEnumAppDomains_do_not_use(/*[in] CLRDATA_ENUM handle*/);
        void GetAppDomainByUniqueID_do_not_use(/*[in] ULONG64 id, [out] IXCLRDataAppDomain** appDomain*/);
        void StartEnumAssemblie_do_not_uses(/*[out] CLRDATA_ENUM* handle*/);
        void EnumAssembly_do_not_use(/*[in, out] CLRDATA_ENUM* handle, [out] IXCLRDataAssembly **assembly*/);
        void EndEnumAssemblies_do_not_use(/*[in] CLRDATA_ENUM handle*/);
        void StartEnumModules_do_not_use(/*[out] CLRDATA_ENUM* handle*/);
        void EnumModule_do_not_use(/*[in, out] CLRDATA_ENUM* handle, [out] IXCLRDataModule **mod*/);
        void EndEnumModules_do_not_use(/*[in] CLRDATA_ENUM handle*/);
        void GetModuleByAddress_do_not_use(/*[in] CLRDATA_ADDRESS address, [out] IXCLRDataModule** mod*/);
        [PreserveSig]
        int StartEnumMethodInstancesByAddress(ulong address, [In, MarshalAs(UnmanagedType.Interface)] object appDomain, out ulong handle);
        [PreserveSig]
        int EnumMethodInstanceByAddress(ref ulong handle, [Out, MarshalAs(UnmanagedType.Interface)] out object method);
        [PreserveSig]
        int EndEnumMethodInstancesByAddress(ulong handle);
        void GetDataByAddress_do_not_use(/*[in] CLRDATA_ADDRESS address, [in] ULONG32 flags, [in] IXCLRDataAppDomain* appDomain, [in] IXCLRDataTask* tlsTask, [in] ULONG32 bufLen, [out] ULONG32 *nameLen, [out, size_is(bufLen)] WCHAR nameBuf[], [out] IXCLRDataValue** value, [out] CLRDATA_ADDRESS* displacement*/);
        void GetExceptionStateByExceptionRecord_do_not_use(/*[in] EXCEPTION_RECORD64* record, [out] IXCLRDataExceptionState **exState*/);
        void TranslateExceptionRecordToNotification_do_not_use();

        [PreserveSig]
        int Request(uint reqCode, uint inBufferSize,
                    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] inBuffer, uint outBufferSize,
                    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] outBuffer);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("ECD73800-22CA-4b0d-AB55-E9BA7E6318A5")]
    public interface IXCLRDataMethodInstance
    {
        void GetTypeInstance_do_not_use(/*[out] IXCLRDataTypeInstance **typeInstance*/);
        void GetDefinition_do_not_use(/*[out] IXCLRDataMethodDefinition **methodDefinition*/);

        /*
         * Get the metadata token and scope.
         */
        void GetTokenAndScope(out uint mdToken, [Out, MarshalAs(UnmanagedType.Interface)] out object module);

        void GetName_do_not_use(/*[in] ULONG32 flags,
                        [in] ULONG32 bufLen,
                        [out] ULONG32 *nameLen,
                        [out, size_is(bufLen)] WCHAR nameBuf[]*/);
        void GetFlags_do_not_use(/*[out] ULONG32* flags*/);
        void IsSameObject_do_not_use(/*[in] IXCLRDataMethodInstance* method*/);
        void GetEnCVersion_do_not_use(/*[out] ULONG32* version*/);
        void GetNumTypeArguments_do_not_use(/*[out] ULONG32* numTypeArgs*/);
        void GetTypeArgumentByIndex_do_not_use(/*[in] ULONG32 index,
                                       [out] IXCLRDataTypeInstance** typeArg*/);

        /*
         * Access the IL <-> address mapping information.
         */
        void GetILOffsetsByAddress(ulong address, uint offsetsLen, out uint offsetsNeeded, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1)] uint[] ilOffsets);

        void GetAddressRangesByILOffset(uint ilOffset, uint rangesLen, out uint rangesNeeded, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1)] uint[] addressRanges);

        [PreserveSig]
        int GetILAddressMap(uint mapLen, out uint mapNeeded, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] ILToNativeMap[] map);

        void StartEnumExtents_do_not_use(/*[out] CLRDATA_ENUM* handle*/);
        void EnumExtent_do_not_use(/*[in, out] CLRDATA_ENUM* handle,
                           [out] CLRDATA_ADDRESS_RANGE* extent*/);
        void EndEnumExtents_do_not_use(/*[in] CLRDATA_ENUM handle*/);
    
        void Request_do_not_use(/*[in] ULONG32 reqCode,
                        [in] ULONG32 inBufferSize,
                        [in, size_is(inBufferSize)] BYTE* inBuffer,
                        [in] ULONG32 outBufferSize,
                        [out, size_is(outBufferSize)] BYTE* outBuffer*/);

        void GetRepresentativeEntryAddress_do_not_use(/*[out] CLRDATA_ADDRESS* addr*/);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("A5B0BEEA-EC62-4618-8012-A24FFC23934C")]
    public interface IXCLRDataTask
    {
        void GetProcess_do_not_use();
        void GetCurrentAppDomain_do_not_use();
        void GetUniqueID_do_not_use();
        void GetFlags_do_not_use();
        void IsSameObject_do_not_use();
        void GetManagedObject_do_not_use();
        void GetDesiredExecutionState_do_not_use();
        void SetDesiredExecutionState_do_not_use();

        /*
         * Create a stack walker to walk this task's stack. The
         * flags parameter takes a bitfield of values from the
         * CLRDataSimpleFrameType enum.
         */
        [PreserveSig]
        int CreateStackWalk(uint flags, [Out, MarshalAs(UnmanagedType.IUnknown)] out object stackwalk);

        void GetOSThreadID_do_not_use();
        void GetContext_do_not_use();
        void SetContext_do_not_use();
        void GetCurrentExceptionState_do_not_use();
        void Request_do_not_use();
        void GetName_do_not_use();
        void GetLastExceptionState_do_not_use();
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("E59D8D22-ADA7-49a2-89B5-A415AFCFC95F")]
    public interface IXCLRDataStackWalk
    {
        [PreserveSig]
        int GetContext(uint contextFlags, uint contextBufSize, out uint contextSize, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] buffer);

        void SetContext_do_not_use();
        [PreserveSig]
        int Next();

        /*
         * Return the number of bytes skipped by the last call to Next().
         * If Next() moved to the very next frame, outputs 0.
         *
         * Note that calling GetStackSizeSkipped() after any function other
         * than Next() has no meaning.
         */
        void GetStackSizeSkipped_do_not_use();

        /* 
         * Return information about the type of the current frame
         */
        void GetFrameType_do_not_use();
        [PreserveSig]
        int GetFrame([Out, MarshalAs(UnmanagedType.IUnknown)] out object frame);

        [PreserveSig]
        int Request(uint reqCode, uint inBufferSize, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] inBuffer,
                    uint outBufferSize, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] outBuffer);

        void SetContext2_do_not_use();
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("271498C2-4085-4766-BC3A-7F8ED188A173")]
    public interface IXCLRDataFrame
    {
        void GetFrameType_do_not_use();
        void GetContext_do_not_use();
        void GetAppDomain_do_not_use();

        [PreserveSig]
        int GetNumArguments(out uint numArgs);

        [PreserveSig]
        int GetArgumentByIndex(
            uint index,
            [Out, MarshalAs(UnmanagedType.IUnknown)] out object arg,
            uint bufLen,
            out uint nameLen,
            [Out, MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 2)] StringBuilder name);

        [PreserveSig]
        int GetNumLocalVariables(out uint numLocals);

        [PreserveSig]
        int GetLocalVariableByIndex(
            uint index,
            [Out, MarshalAs(UnmanagedType.IUnknown)] out object localVariable,
            uint bufLen,
            out uint nameLen,
            [Out, MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 2)] StringBuilder name);

        [PreserveSig]
        int GetCodeName(uint flags, uint bufLen, out uint nameLen,
            [Out, MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 1)] StringBuilder nameBuf);

        void GetMethodInstance_do_not_use();
        void Request_do_not_use();
        void GetNumTypeArguments_do_not_use();
        void GetTypeArgumentByIndex_do_not_use();
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("4675666C-C275-45b8-9F6C-AB165D5C1E09")]
    public interface IXCLRDataTypeDefinition
    {
        void GetModule_do_not_use();
        void StartEnumMethodDefinitions_do_not_use();
        void EnumMethodDefinition_do_not_use();
        void EndEnumMethodDefinitions_do_not_use();

        void StartEnumMethodDefinitionsByName_do_not_use();
        void EnumMethodDefinitionByName_do_not_use();
        void EndEnumMethodDefinitionsByName_do_not_use();

        void GetMethodDefinitionByToken_do_not_use();

        void StartEnumInstances_do_not_use();
        void EnumInstance_do_not_use();
        void EndEnumInstances_do_not_use();

        [PreserveSig]
        int GetName(uint flags, uint bufLen, out uint nameLen,
            [Out, MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 1)] StringBuilder nameBuf);

        [PreserveSig]
        int GetTokenAndScope(
            out int token, //mdTypeDef*
            [Out, MarshalAs(UnmanagedType.IUnknown)] out object mod //IXCLRDataModule**
            );

        void GetCorElementType_do_not_use();

        [PreserveSig]
        int GetFlags(out uint flags);

        void IsSameObject_do_not_use();

        void Request_do_not_use();

        void GetArrayRank_do_not_use();

        void GetBase_do_not_use();

        void GetNumFields_do_not_use();

        void StartEnumFields_do_not_use();
        void EnumField_do_not_use();
        void EndEnumFields_do_not_use();

        void StartEnumFieldsByName_do_not_use();
        void EnumFieldByName_do_not_use();
        void EndEnumFieldsByName_do_not_use();

        void GetFieldByToken_do_not_use();

        void GetTypeNotification_do_not_use();
        void SetTypeNotification_do_not_use();

        void EnumField2_do_not_use();
        void EnumFieldByName2_do_not_use();
        void GetFieldByToken2_do_not_use();
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("4D078D91-9CB3-4b0d-97AC-28C8A5A82597")]
    public interface IXCLRDataTypeInstance
    {
        void StartEnumMethodInstances_do_not_use();
        void EnumMethodInstance_do_not_use();
        void EndEnumMethodInstances_do_not_use();
        void StartEnumMethodInstancesByName_do_not_use();
        void EnumMethodInstanceByName_do_not_use();
        void EndEnumMethodInstancesByName_do_not_use();
        void GetNumStaticFields_do_not_use();
        void GetStaticFieldByIndex_do_not_use();
        void StartEnumStaticFieldsByName_do_not_use();
        void EnumStaticFieldByName_do_not_use();
        void EndEnumStaticFieldsByName_do_not_use();
        void GetNumTypeArguments_do_not_use();
        void GetTypeArgumentByIndex_do_not_use();

        [PreserveSig]
        int GetName(uint flags, uint bufLen, out uint nameLen,
            [Out, MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 1)] StringBuilder nameBuf
            );

        void GetModule_do_not_use();

        [PreserveSig]
        int GetDefinition([Out, MarshalAs(UnmanagedType.IUnknown)] out object typeDefinition);

        [PreserveSig]
        int GetFlags(out uint flags); // CLRDataTypeFlag

        void IsSameObject_do_not_use();
        void Request_do_not_use();
        void GetNumStaticField2_do_not_use();
        void StartEnumStaticFields_do_not_use();
        void EnumStaticField_do_not_use();
        void EndEnumStaticFields_do_not_use();
        void StartEnumStaticFieldsByName2_do_not_use();
        void EnumStaticFieldByName2_do_not_use();
        void EndEnumStaticFieldsByName2_do_not_use();
        void GetStaticFieldByToken_do_not_use();
        void GetBase_do_not_use();
        void EnumStaticField2_do_not_use();
        void EnumStaticFieldByName3_do_not_use();
        void GetStaticFieldByToken2_do_not_use();
    }

    public enum ClrDataValueLocationFlag : uint
    {
        CLRDATA_VLOC_MEMORY = 0x00000000,
        CLRDATA_VLOC_REGISTER = 0x00000001,
    };

    [Flags]
    public enum CLRDataTypeFlag : uint
    {
        CLRDATA_TYPE_DEFAULT = 0x00000000,

        // Identify particular kinds of types.  These flags
        // are shared between type, field and value.
        CLRDATA_TYPE_IS_PRIMITIVE = 0x00000001,
        CLRDATA_TYPE_IS_VALUE_TYPE = 0x00000002,
        CLRDATA_TYPE_IS_STRING = 0x00000004,
        CLRDATA_TYPE_IS_ARRAY = 0x00000008,
        CLRDATA_TYPE_IS_REFERENCE = 0x00000010,
        CLRDATA_TYPE_IS_POINTER = 0x00000020,
        CLRDATA_TYPE_IS_ENUM = 0x00000040,

        // Alias for all field kinds.
        CLRDATA_TYPE_ALL_KINDS = 0x7f,
    }

    [Flags]
    public enum CLRDataFieldFlag : uint
    {
        CLRDATA_FIELD_DEFAULT = 0x00000000,

        // Identify particular kinds of types.  These flags
        // are shared between type, field and value.
        CLRDATA_FIELD_IS_PRIMITIVE = CLRDataTypeFlag.CLRDATA_TYPE_IS_PRIMITIVE,
        CLRDATA_FIELD_IS_VALUE_TYPE = CLRDataTypeFlag.CLRDATA_TYPE_IS_VALUE_TYPE,
        CLRDATA_FIELD_IS_STRING = CLRDataTypeFlag.CLRDATA_TYPE_IS_STRING,
        CLRDATA_FIELD_IS_ARRAY = CLRDataTypeFlag.CLRDATA_TYPE_IS_ARRAY,
        CLRDATA_FIELD_IS_REFERENCE = CLRDataTypeFlag.CLRDATA_TYPE_IS_REFERENCE,
        CLRDATA_FIELD_IS_POINTER = CLRDataTypeFlag.CLRDATA_TYPE_IS_POINTER,
        CLRDATA_FIELD_IS_ENUM = CLRDataTypeFlag.CLRDATA_TYPE_IS_ENUM,

        // Alias for all field kinds.
        CLRDATA_FIELD_ALL_KINDS = CLRDataTypeFlag.CLRDATA_TYPE_ALL_KINDS,

        // Identify field properties.  These flags are
        // shared between field and value.
        CLRDATA_FIELD_IS_INHERITED = 0x00000080,
        CLRDATA_FIELD_IS_LITERAL = 0x00000100,

        // Identify field storage location.  These flags are
        // shared between field and value.
        CLRDATA_FIELD_FROM_INSTANCE = 0x00000200,
        CLRDATA_FIELD_FROM_TASK_LOCAL = 0x00000400,
        CLRDATA_FIELD_FROM_STATIC = 0x00000800,

        // Alias for all types of field locations.
        CLRDATA_FIELD_ALL_LOCATIONS = 0x00000e00,
        // Alias for all fields from all locations.
        CLRDATA_FIELD_ALL_FIELDS = 0x00000eff,
    }

    [Flags]
    public enum CLRDataValueFlag : uint
    {
        Invalid = uint.MaxValue,

        CLRDATA_VALUE_DEFAULT = 0x00000000,

        // Identify particular kinds of types.  These flags
        // are shared between type, field and value.
        CLRDATA_VALUE_IS_PRIMITIVE = CLRDataTypeFlag.CLRDATA_TYPE_IS_PRIMITIVE,
        CLRDATA_VALUE_IS_VALUE_TYPE = CLRDataTypeFlag.CLRDATA_TYPE_IS_VALUE_TYPE,
        CLRDATA_VALUE_IS_STRING = CLRDataTypeFlag.CLRDATA_TYPE_IS_STRING,
        CLRDATA_VALUE_IS_ARRAY = CLRDataTypeFlag.CLRDATA_TYPE_IS_ARRAY,
        CLRDATA_VALUE_IS_REFERENCE = CLRDataTypeFlag.CLRDATA_TYPE_IS_REFERENCE,
        CLRDATA_VALUE_IS_POINTER = CLRDataTypeFlag.CLRDATA_TYPE_IS_POINTER,
        CLRDATA_VALUE_IS_ENUM = CLRDataTypeFlag.CLRDATA_TYPE_IS_ENUM,

        // Alias for all value kinds.
        CLRDATA_VALUE_ALL_KINDS = CLRDataTypeFlag.CLRDATA_TYPE_ALL_KINDS,

        // Identify field properties.  These flags are
        // shared between field and value.
        CLRDATA_VALUE_IS_INHERITED = CLRDataFieldFlag.CLRDATA_FIELD_IS_INHERITED,
        CLRDATA_VALUE_IS_LITERAL = CLRDataFieldFlag.CLRDATA_FIELD_IS_LITERAL,

        // Identify field storage location.  These flags are
        // shared between field and value.
        CLRDATA_VALUE_FROM_INSTANCE = CLRDataFieldFlag.CLRDATA_FIELD_FROM_INSTANCE,
        CLRDATA_VALUE_FROM_TASK_LOCAL = CLRDataFieldFlag.CLRDATA_FIELD_FROM_TASK_LOCAL,
        CLRDATA_VALUE_FROM_STATIC = CLRDataFieldFlag.CLRDATA_FIELD_FROM_STATIC,

        // Alias for all types of field locations.
        CLRDATA_VALUE_ALL_LOCATIONS = CLRDataFieldFlag.CLRDATA_FIELD_ALL_LOCATIONS,
        // Alias for all fields from all locations.
        CLRDATA_VALUE_ALL_FIELDS = CLRDataFieldFlag.CLRDATA_FIELD_ALL_FIELDS,

        // Identify whether the value is a boxed object.
        CLRDATA_VALUE_IS_BOXED = 0x00001000,
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("96EC93C7-1000-4e93-8991-98D8766E6666")]
    public interface IXCLRDataValue
    {
        [PreserveSig]
        int GetFlags(out uint flags); // returns values from CLRDataValueFlag

        void GetAddress_do_not_use();

        [PreserveSig]
        int GetSize(out ulong size);

        [PreserveSig]
        int GetBytes(
            uint bufLen,
            out uint dataSize,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] byte[] buffer);

        void SetBytes_do_not_use();

        [PreserveSig]
        int GetType([Out, MarshalAs(UnmanagedType.IUnknown)] out object typeInstance);

        void GetNumFields_do_not_use();
        void GetFieldByIndex_do_not_use();
        void Request_do_not_use();
        void GetNumFields2_do_not_use();
        void StartEnumFields_do_not_use();
        void EnumField_do_not_use();
        void EndEnumFields_do_not_use();
        void StartEnumFieldsByName_do_not_use();
        void EnumFieldByName_do_not_use();
        void EndEnumFieldsByName_do_not_use();
        void GetFieldByToken_do_not_use();

        [PreserveSig]
        int GetAssociatedValue(out object assocValue);

        [PreserveSig]
        int GetAssociatedType([Out, MarshalAs(UnmanagedType.IUnknown)] out object assocType);

        void GetString_do_not_use();
        void GetArrayProperties_do_not_use();
        void GetArrayElement_do_not_use();
        void EnumField2_do_not_use();
        void EnumFieldByName2_do_not_use();
        void GetFieldByToken2_do_not_use();

        [PreserveSig]
        int GetNumLocations(out uint numLocs);

        [PreserveSig]
        int GetLocationByIndex(uint loc, out uint flags, out ulong arg);
    }
    
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("88E32849-0A0A-4cb0-9022-7CD2E9E139E2")]
    public interface IXCLRDataModule
    {
        /*
         * Enumerate assemblies this module is part of.
         * Module-to-assembly is an enumeration as a
         * shared module might be part of more than one assembly.
         */
        void StartEnumAssemblies_do_not_use(/*[out] CLRDATA_ENUM* handle*/);
        void EnumAssembly_do_not_use(/*[in, out] CLRDATA_ENUM* handle, [out] IXCLRDataAssembly **assembly*/);
        void EndEnumAssemblies_do_not_use(/*[in] CLRDATA_ENUM handle*/);

        /*
         * Enumerate types in this module.
         */
        void StartEnumTypeDefinitions_do_not_use(/*[out] CLRDATA_ENUM* handle*/);
        void EnumTypeDefinition_do_not_use(/*[in, out] CLRDATA_ENUM* handle, [out] IXCLRDataTypeDefinition **typeDefinition*/);
        void EndEnumTypeDefinitions_do_not_use(/*[in] CLRDATA_ENUM handle*/);

        void StartEnumTypeInstances_do_not_use(/*[in] IXCLRDataAppDomain* appDomain, [out] CLRDATA_ENUM* handle*/);
        void EnumTypeInstance_do_not_use(/*[in, out] CLRDATA_ENUM* handle, [out] IXCLRDataTypeInstance **typeInstance*/);
        void EndEnumTypeInstances_do_not_use(/*[in] CLRDATA_ENUM handle*/);

        /*
         * Look up types by name.
         */
        void StartEnumTypeDefinitionsByName_do_not_use(/*[in] LPCWSTR name, [in] ULONG32 flags, [out] CLRDATA_ENUM* handle*/);
        void EnumTypeDefinitionByName_do_not_use(/*[in,out] CLRDATA_ENUM* handle, [out] IXCLRDataTypeDefinition** type*/);
        void EndEnumTypeDefinitionsByName_do_not_use(/*[in] CLRDATA_ENUM handle*/);

        void StartEnumTypeInstancesByName_do_not_use(/*[in] LPCWSTR name, [in] ULONG32 flags, [in] IXCLRDataAppDomain* appDomain, [out] CLRDATA_ENUM* handle*/);
        void EnumTypeInstanceByName_do_not_use(/*[in,out] CLRDATA_ENUM* handle, [out] IXCLRDataTypeInstance** type*/);
        void EndEnumTypeInstancesByName_do_not_use(/*[in] CLRDATA_ENUM handle*/);

        /*
         * Get a type definition by metadata token.
         */
        void GetTypeDefinitionByToken_do_not_use(/*[in] mdTypeDef token, [out] IXCLRDataTypeDefinition** typeDefinition*/);
    
        /*
         * Look up methods by name.
         */
        void StartEnumMethodDefinitionsByName_do_not_use(/*[in] LPCWSTR name, [in] ULONG32 flags, [out] CLRDATA_ENUM* handle*/);
        void EnumMethodDefinitionByName_do_not_use(/*[in,out] CLRDATA_ENUM* handle, [out] IXCLRDataMethodDefinition** method*/);
        void EndEnumMethodDefinitionsByName_do_not_use(/*[in] CLRDATA_ENUM handle*/);
    
        void StartEnumMethodInstancesByName_do_not_use(/*[in] LPCWSTR name, [in] ULONG32 flags, [in] IXCLRDataAppDomain* appDomain, [out] CLRDATA_ENUM* handle*/);
        void EnumMethodInstanceByName_do_not_use(/*[in,out] CLRDATA_ENUM* handle, [out] IXCLRDataMethodInstance** method*/);
        void EndEnumMethodInstancesByName_do_not_use(/*[in] CLRDATA_ENUM handle*/);
    
        /*
         * Get a method definition by metadata token.
         */
        void GetMethodDefinitionByToken_do_not_use(/*[in] mdMethodDef token, [out] IXCLRDataMethodDefinition** methodDefinition*/);

        /*
         * Look up pieces of data by name.
         */
        void StartEnumDataByName_do_not_use(/*[in] LPCWSTR name, [in] ULONG32 flags, [in] IXCLRDataAppDomain* appDomain, [in] IXCLRDataTask* tlsTask, [out] CLRDATA_ENUM* handle*/);
        void EnumDataByName_do_not_use(/*[in,out] CLRDATA_ENUM* handle, [out] IXCLRDataValue** value*/);
        void EndEnumDataByName_do_not_use(/*[in] CLRDATA_ENUM handle*/);
    
        /*
         * Get the module's base name.
         */
        [PreserveSig]
        int GetName(
            uint bufLen,
            out uint nameLen,
            [Out, MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 1)] StringBuilder name
            );

        /*
         * Get the full path and filename for the module,
         * if there is one.
         */
        void GetFileName_do_not_use(/*[in] ULONG32 bufLen, [out] ULONG32 *nameLen, [out, size_is(bufLen)] WCHAR name[]*/);

        /*
         * Get state flags, defined in CLRDataModuleFlag.
         */
        void GetFlags_do_not_use(/*[out] ULONG32* flags*/);
    
        /*
         * Determine whether the given interface represents
         * the same target state.
         */
        void IsSameObject_do_not_use(/*[in] IXCLRDataModule* mod*/);
    
        /*
         * Get the memory regions associated with this module.
         */
        void StartEnumExtents(out ulong handle);
        void EnumExtent(ref ulong handle, out CLRDATA_MODULE_EXTENT extent);
        void EndEnumExtents(ulong handle);

        void Request_do_not_use(/*[in] ULONG32 reqCode, [in] ULONG32 inBufferSize, [in, size_is(inBufferSize)] BYTE* inBuffer, [in] ULONG32 outBufferSize, [out, size_is(outBufferSize)] BYTE* outBuffer*/);

        /*
         * Enumerate the app domains using this module.
         */
        void StartEnumAppDomains_do_not_use(/*[out] CLRDATA_ENUM* handle*/);
        void EnumAppDomain_do_not_use(/*[in, out] CLRDATA_ENUM* handle, [out] IXCLRDataAppDomain** appDomain*/);
        void EndEnumAppDomains_do_not_use(/*[in] CLRDATA_ENUM handle*/);

        /*
         * Get the module's version ID.
         * Requires revision 3.
         */
        void GetVersionId_do_not_use(/*[out] GUID* vid*/);
    }

    public enum ModuleExtentType
    {
        CLRDATA_MODULE_PE_FILE,
        CLRDATA_MODULE_PREJIT_FILE,
        CLRDATA_MODULE_MEMORY_STREAM,
        CLRDATA_MODULE_OTHER
    }

    public struct CLRDATA_MODULE_EXTENT
    {
        public ulong baseAddress;
        public uint length;
        public ModuleExtentType type;
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("aa8fa804-bc05-4642-b2c5-c353ed22fc63")]
    public interface IMetadataLocator
    {
        [PreserveSig]
        int GetMetadata([In, MarshalAs(UnmanagedType.LPWStr)] string imagePath,
                        uint imageTimestamp,
                        uint imageSize,
                        IntPtr mvid, // (guid, unused)
                        uint mdRva,
                        uint flags,  // unused
                        uint bufferSize,
                        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 6)]
                        byte[] buffer,
                        IntPtr ptr);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("3E11CCEE-D08B-43e5-AF01-32717A64DA03")]
    public interface IDacDataTarget
    {
        void GetMachineType(out IMAGE_FILE_MACHINE machineType);

        void GetPointerSize(out uint pointerSize);

        void GetImageBase([In, MarshalAs(UnmanagedType.LPWStr)] string imagePath, out ulong baseAddress);

        [PreserveSig]
        int ReadVirtual(ulong address,
                        IntPtr buffer,
                        int bytesRequested,
                        out int bytesRead);


        void WriteVirtual(ulong address,
                         [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buffer,
                         uint bytesRequested,
                         out uint bytesWritten);

        void GetTLSValue(uint threadID,
                            uint index,
                            out ulong value);

        void SetTLSValue(uint threadID,
                        uint index,
                        ulong value);

        void GetCurrentThreadID(out uint threadID);

        void GetThreadContext(uint threadID,
                             uint contextFlags,
                             uint contextSize,
                             IntPtr context);
        void SetThreadContext(uint threadID,
                              uint contextSize,
                              IntPtr context);

        void Request(uint reqCode,
                    uint inBufferSize,
                    IntPtr inBuffer,
                    IntPtr outBufferSize,
                    out IntPtr outBuffer);
    }
}
