using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;
using RGiesecke.DllExport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMsos
    {
        string Echo(string message);
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [ComDefaultInterface(typeof(IMsos))]
    public class Msos : IMsos
    {
        private DataTarget _target;

        internal Msos(object debugClient)
        {
            _target = DataTarget.CreateFromDebuggerInterface((IDebugClient)debugClient);
        }

        public string Echo(string message)
        {
            return "<< " + message + " >>";
        }
    }

    static class Exports
    {
        [DllExport("CreateMsos", CallingConvention = CallingConvention.StdCall)]
        public static void CreateMsos(
            [In] [MarshalAs(UnmanagedType.IUnknown)] object debugClient,
            [Out] [MarshalAs(UnmanagedType.Interface)] out IMsos msos)
        {
            msos = new Msos(debugClient);
        }
    }
}
