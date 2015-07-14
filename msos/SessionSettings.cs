using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msos
{
    [Verb(".hyperlinks", HelpText = "Configures hyperlink options in command output.")]
    class HyperlinkSettings : ICommand
    {
        [Option("enable", SetName = "enable", HelpText = "Enables hyperlink output.")]
        public bool Enable { get; set; }

        [Option("disable", SetName = "disable", HelpText = "Disables hyperlink output.")]
        public bool Disable { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            if (Enable)
            {
                context.HyperlinkOutput = true;
            }
            if (Disable)
            {
                context.HyperlinkOutput = false;
            }
        }
    }


    [Verb(".paging", HelpText = "Configures command output paging.")]
    class PagingSettings : ICommand
    {
        [Option("disable", HelpText = "Disable paging.")]
        public bool Disable { get; set; }

        [Option("rows", HelpText = "The number of rows to print before pausing for user confirmation.")]
        public uint RowsPerPage { get; set; }

        public void Execute(CommandExecutionContext context)
        {
            if (Disable)
            {
                context.Printer.RowsPerPage = 0;
                return;
            }

            if (RowsPerPage == 0)
            {
                context.WriteError("Can't have 0 rows per page. Use --disable to disable paging altogether.");
                return;
            }

            context.Printer.RowsPerPage = RowsPerPage;
        }
    }
}
