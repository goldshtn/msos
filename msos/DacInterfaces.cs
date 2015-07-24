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
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name);

        [PreserveSig]
        int GetNumLocalVariables(out uint numLocals);

        [PreserveSig]
        int GetLocalVariableByIndex(
            uint index,
            [Out, MarshalAs(UnmanagedType.IUnknown)] out object localVariable,
            uint bufLen,
            out uint nameLen,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name);

        void GetCodeName_do_not_use();
        void GetMethodInstance_do_not_use();
        void Request_do_not_use();
        void GetNumTypeArguments_do_not_use();
        void GetTypeArgumentByIndex_do_not_use();
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
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder nameBuf
            );

        void GetModule_do_not_use();
        void GetDefinition_do_not_use();
        void GetFlags_do_not_use();
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
        int GetNumLocations(uint numLocs);

        [PreserveSig]
        int GetLocationByIndex(uint loc, out ulong flags, out ulong arg);
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
