using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    abstract class PrinterBase : IPrinter
    {
        public void WriteInfo(string format, params object[] args)
        {
            WriteInfo(String.Format(format, args));
        }

        public void WriteCommandOutput(string format, params object[] args)
        {
            WriteCommandOutput(String.Format(format, args));
        }

        public void WriteError(string format, params object[] args)
        {
            WriteError(String.Format(format, args));
        }

        public void WriteWarning(string format, object[] args)
        {
            WriteWarning(String.Format(format, args));
        }


        public abstract void WriteInfo(string value);
        public abstract void WriteCommandOutput(string value);
        public abstract void WriteError(string value);
        public abstract void WriteWarning(string value);

        public virtual void ClearScreen()
        {
        }

        public virtual void Dispose()
        {
        }
    }
}
