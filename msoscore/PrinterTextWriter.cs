using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    class PrinterTextWriter : TextWriter
    {
        private IPrinter _printer;

        public PrinterTextWriter(IPrinter printer)
        {
            _printer = printer;
        }

        public override Encoding Encoding
        {
            get { return Encoding.Unicode; }
        }

        public override void Write(char value)
        {
            Write(value.ToString());
        }

        public override void Write(string value)
        {
            _printer.WriteCommandOutput(value);
        }
    }
}
