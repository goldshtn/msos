using msos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace msos_server.Models
{
    public class TargetModel
    {
        internal TargetModel(HtmlPrinter printer, string sessionId)
        {
            Output = printer.Html;
            SessionId = sessionId;
        }

        public string Output { get; private set; }
        public string SessionId { get; private set; }
    }
}