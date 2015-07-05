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
        [Option('z', HelpText = "The dump file to open.")]
        public string DumpFile { get; set; }

        [Option("pn", HelpText = "The process name to attach to. Fails if there are multiple processes with that name.")]
        public string ProcessName { get; set; }

        [Option("pid", HelpText = "The process id to attach to.")]
        public int ProcessId { get; set; }
    }
}
