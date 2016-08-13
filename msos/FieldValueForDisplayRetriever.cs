using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.RuntimeExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    interface IFieldValueForDisplayRetriever<TField> where TField : ClrField
    {
        string GetFieldValue(TField field, bool inner = false);
        ulong GetFieldAddress(TField field, bool inner = false);
        string GetDisplayString(TField field, int offset = 0, string baseName = "", bool inner = false);
    }

    class InstanceFieldValueForDisplayRetriever : IFieldValueForDisplayRetriever<ClrInstanceField>
    {
        private ulong _objPtr;

        public InstanceFieldValueForDisplayRetriever(ulong objPtr)
        {
            _objPtr = objPtr;
        }

        public string GetFieldValue(ClrInstanceField field, bool inner = false)
        {
            if (field.IsObjectReferenceNotString())
                return String.Format("{0:x16}", (ulong)field.GetValue(_objPtr, inner));

            if (field.HasSimpleValue)
                return field.GetValue(_objPtr, inner).ToStringOrNull();

            return String.Format("{0:x16}", field.GetAddress(_objPtr, inner));
        }

        public ulong GetFieldAddress(ClrInstanceField field, bool inner = false)
        {
            return field.GetAddress(_objPtr, inner);
        }

        public string GetDisplayString(ClrInstanceField field, int offset = 0, string baseName = "", bool inner = false)
        {
            string fieldValueDisplay = GetFieldValue(field, inner);
            return String.Format("{0,-8:x} {1,-20} {2,-3} {3,-10} {4,-20:x16} {5}",
                offset + field.Offset, field.GetFieldTypeNameTrimmed(),
                (field.IsOfPrimitiveType() || field.IsOfValueClass()) ? 1 : 0,
                "instance", fieldValueDisplay, baseName + field.Name);
        }
    }

    class ThreadStaticFieldValueForDisplayRetriever : IFieldValueForDisplayRetriever<ClrThreadStaticField>
    {
        private ClrAppDomain _appDomain;
        private ClrThread _thread;

        public ThreadStaticFieldValueForDisplayRetriever(ClrAppDomain appDomain, ClrThread thread)
        {
            _appDomain = appDomain;
            _thread = thread;
        }

        public string GetFieldValue(ClrThreadStaticField field, bool inner = false)
        {
            if (field.IsObjectReferenceNotString())
            {
                object value = field.GetValue(_appDomain, _thread) ?? 0UL;
                return String.Format("{0:x16}", (ulong)value);
            }

            if (field.HasSimpleValue)
                return field.GetValue(_appDomain, _thread).ToStringOrNull();

            return String.Format("{0:x16}", field.GetAddress(_appDomain, _thread));
        }

        public ulong GetFieldAddress(ClrThreadStaticField field, bool inner = false)
        {
            return field.GetAddress(_appDomain, _thread);
        }

        public string GetDisplayString(ClrThreadStaticField field, int offset = 0, string baseName = "", bool inner = false)
        {
            string fieldValueDisplay = GetFieldValue(field, inner);
            return String.Format("   >> Domain:Thread:Value  {0:x16}:{1}:{2} <<",
                _appDomain.Address, _thread.ManagedThreadId, fieldValueDisplay);
        }
    }

    class StaticFieldValueForDisplayRetriever : IFieldValueForDisplayRetriever<ClrStaticField>
    {
        private ClrAppDomain _appDomain;

        public StaticFieldValueForDisplayRetriever(ClrAppDomain appDomain)
        {
            _appDomain = appDomain;
        }

        public string GetFieldValue(ClrStaticField field, bool inner = false)
        {
            if (field.IsObjectReferenceNotString())
            {
                object value = field.GetValue(_appDomain) ?? 0UL;
                return String.Format("{0:x16}", (ulong)value);
            }

            if (field.HasSimpleValue)
                return field.GetValue(_appDomain).ToStringOrNull();

            return String.Format("{0:x16}", field.GetAddress(_appDomain));
        }

        public ulong GetFieldAddress(ClrStaticField field, bool inner = false)
        {
            return field.GetAddress(_appDomain);
        }

        public string GetDisplayString(ClrStaticField field, int offset = 0, string baseName = "", bool inner = false)
        {
            string fieldValueDisplay = GetFieldValue(field, inner);
            return String.Format("   >> Domain:Value  {0:x16}:{1} <<", _appDomain.Address, fieldValueDisplay);
        }
    }
}
