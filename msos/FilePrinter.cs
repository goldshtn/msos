using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    class FilePrinter : PrinterBase
    {
        private StreamWriter _output;

        public FilePrinter(string fileName)
        {
            _output = File.CreateText(fileName);
        }

        public override void WriteInfo(string value)
        {
            _output.WriteLine("INFO: " + value);
        }

        public override void WriteCommandOutput(string value)
        {
            _output.WriteLine(value);
        }

        public override void WriteError(string value)
        {
            _output.WriteLine("ERROR: " + value);
        }

        public override void WriteWarning(string value)
        {
            _output.WriteLine("WARNING: " + value);
        }

        public override void Dispose()
        {
            _output.Dispose();
        }
    }
}
