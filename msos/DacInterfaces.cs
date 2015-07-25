using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    // These wrappers are partially copied from ClrMD https://github.com/Microsoft/dotnetsamples/blob/master/Microsoft.Diagnostics.Runtime/CLRMD/ClrMemDiag/dacprivate.cs
    // and partially adapted from xclrdata.idl in the CoreCLR sources https://github.com/dotnet/coreclr/blob/master/src/inc/xclrdata.idl

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("5c552ab6-fc09-4cb3-8e36-22fa03c798b7")]
    interface IXCLRDataProcess
    {
        void Flush();

        void StartEnumTasks_do_not_use();
        void EnumTask_do_not_use();
        void EndEnumTasks_do_not_use();
        [PreserveSig]
        int GetTaskByOSThreadID(uint id, [Out, MarshalAs(UnmanagedType.IUnknown)] out object task);
        void GetTaskByUniqueID_do_not_use();
        void GetFlags_do_not_use();
        void IsSameObject_do_not_use();
        void GetManagedObject_do_not_use();
        void GetDesiredExecutionState_do_not_use();
        void SetDesiredExecutionState_do_not_use();
        void GetAddressType_do_not_use();
        void GetRuntimeNameByAddress_do_not_use();
        void StartEnumAppDomains_do_not_use();
        void EnumAppDomain_do_not_use();
        void EndEnumAppDomains_do_not_use();
        void GetAppDomainByUniqueID_do_not_use();
        void StartEnumAssemblie_do_not_uses();
        void EnumAssembly_do_not_use();
        void EndEnumAssemblies_do_not_use();
        void StartEnumModules_do_not_use();
        void EnumModule_do_not_use();
        void EndEnumModules_do_not_use();
        void GetModuleByAddress_do_not_use();
        [PreserveSig]
        int StartEnumMethodInstancesByAddress(ulong address, [In, MarshalAs(UnmanagedType.Interface)] object appDomain, out ulong handle);
        [PreserveSig]
        int EnumMethodInstanceByAddress(ref ulong handle, [Out, MarshalAs(UnmanagedType.Interface)] out object method);
        [PreserveSig]
        int EndEnumMethodInstancesByAddress(ulong handle);
        void GetDataByAddress_do_not_use();
        void GetExceptionStateByExceptionRecord_do_not_use();
        void TranslateExceptionRecordToNotification_do_not_use();

        [PreserveSig]
        int Request(uint reqCode, uint inBufferSize,
                    [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] inBuffer, uint outBufferSize,
                    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] outBuffer);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("A5B0BEEA-EC62-4618-8012-A24FFC23934C")]
    interface IXCLRDataTask
    {
        void GetProcess_do_not_use();
        void GetCurrentAppDomain_do_not_use();
        void GetUniqueID_do_not_use();
        void GetFlags_do_not_use();
        void IsSameObject_do_not_use();
        void GetManagedObject_do_not_use();
        void GetDesiredExecutionState_do_not_use();
        void SetDesiredExecutionState_do_not_use();

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

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("271498C2-4085-4766-BC3A-7F8ED188A173")]
    interface IXCLRDataFrame
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

    enum CLRDataTypeFlag : uint
    {
        CLRDATA_TYPE_IS_PRIMITIVE  = 0x00000001,
        CLRDATA_TYPE_IS_VALUE_TYPE = 0x00000002,
        CLRDATA_TYPE_IS_STRING     = 0x00000004,
        CLRDATA_TYPE_IS_ARRAY      = 0x00000008,
        CLRDATA_TYPE_IS_REFERENCE  = 0x00000010,
        CLRDATA_TYPE_IS_POINTER    = 0x00000020,
        CLRDATA_TYPE_IS_ENUM       = 0x00000040,
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("4675666C-C275-45b8-9F6C-AB165D5C1E09")]
    interface IXCLRDataTypeDefinition
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

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("88E32849-0A0A-4cb0-9022-7CD2E9E139E2")]
    interface IXCLRDataModule
    {
        void StartEnumAssemblies_do_not_use();
        void EnumAssembly_do_not_use();
        void EndEnumAssemblies_do_not_use();
        void StartEnumTypeDefinitions_do_not_use();
        void EnumTypeDefinition_do_not_use();
        void EndEnumTypeDefinitions_do_not_use();
        void StartEnumTypeInstances_do_not_use();
        void EnumTypeInstance_do_not_use();
        void EndEnumTypeInstances_do_not_use();
        void StartEnumTypeDefinitionsByName_do_not_use();
        void EnumTypeDefinitionByName_do_not_use();
        void EndEnumTypeDefinitionsByName_do_not_use();
        void StartEnumTypeInstancesByName_do_not_use();
        void EnumTypeInstanceByName_do_not_use();
        void EndEnumTypeInstancesByName_do_not_use();
        void GetTypeDefinitionByToken_do_not_use();
        void StartEnumMethodDefinitionsByName_do_not_use();
        void EnumMethodDefinitionByName_do_not_use();
        void EndEnumMethodDefinitionsByName_do_not_use();
        void StartEnumMethodInstancesByName_do_not_use();
        void EnumMethodInstanceByName_do_not_use();
        void EndEnumMethodInstancesByName_do_not_use();
        void GetMethodDefinitionByToken_do_not_use();
        void StartEnumDataByName_do_not_use();
        void EnumDataByName_do_not_use();
        void EndEnumDataByName_do_not_use();

        [PreserveSig]
        int GetName(
            uint bufLen,
            out uint nameLen,
            [Out, MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 1)] StringBuilder name
            );

        void GetFileName_do_not_use();
        void GetFlags_do_not_use();
        void IsSameObject_do_not_use();
        void StartEnumExtents_do_not_use();
        void EnumExtent_do_not_use();
        void EndEnumExtents_do_not_use();
        void Request_do_not_use();
        void StartEnumAppDomains_do_not_use();
        void EnumAppDomain_do_not_use();
        void EndEnumAppDomains_do_not_use();
        void GetVersionId_do_not_use();
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("4D078D91-9CB3-4b0d-97AC-28C8A5A82597")]
    interface IXCLRDataTypeInstance
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

    enum ClrDataValueLocationFlag : uint
	{
	    CLRDATA_VLOC_MEMORY   = 0x00000000,
	    CLRDATA_VLOC_REGISTER = 0x00000001,
	};

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("96EC93C7-1000-4e93-8991-98D8766E6666")]
    interface IXCLRDataValue
    {
        void GetFlags_do_not_use();
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
        void GetAssociatedValue_do_not_use();

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

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("E59D8D22-ADA7-49a2-89B5-A415AFCFC95F")]
    interface IXCLRDataStackWalk
    {
        [PreserveSig]
        int GetContext(uint contextFlags, uint contextBufSize, out uint contextSize, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] buffer);

        void SetContext_do_not_use();
        [PreserveSig]
        int Next();

        void GetStackSizeSkipped_do_not_use();
        void GetFrameType_do_not_use();

        [PreserveSig]
        int GetFrame([Out, MarshalAs(UnmanagedType.IUnknown)] out object frame);

        [PreserveSig]
        int Request(uint reqCode, uint inBufferSize, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] inBuffer,
                    uint outBufferSize, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] outBuffer);

        void SetContext2_do_not_use();
    }
}
