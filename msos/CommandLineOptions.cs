using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    class CommandLineOptions
    {
        [Option('z', Required = true, HelpText = "The dump file to open.")]
        public string DumpFile { get; set; }
    }
}
