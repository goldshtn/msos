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
        public string Echo(string message)
        {
            return "<< " + message + " >>";
        }
    }

    // TODO This must be in a DLL. In fact, it's time for a split. The msos core should be a 
    // DLL, and msos.exe should be just the Program class.
    // Motivation: can't LoadLibrary an .exe and then use it as code...
    static class Exports
    {
        [DllExport("CreateMsos", CallingConvention = CallingConvention.StdCall)]
        public static void CreateMsos([Out] [MarshalAs(UnmanagedType.Interface)] out IMsos msos)
        {
            msos = new Msos();
        }
    }
}
