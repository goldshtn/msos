using msos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace msos_server.Models
{
    class AnalysisSession
    {
        public string Id { get; private set; }
        public CommandExecutionContext Context { get; private set; }
        public AnalysisTarget Target { get; private set; }
        public HtmlPrinter Printer { get; private set; }

        public AnalysisSession(CommandExecutionContext context, AnalysisTarget target, HtmlPrinter printer)
        {
            Id = Guid.NewGuid().ToString();
            Context = context;
            Target = target;
            Printer = printer;
        }
    }
}