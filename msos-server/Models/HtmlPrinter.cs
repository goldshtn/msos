using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace msos_server.Models
{
    class HtmlPrinter : msos.PrinterBase
    {
        private StringBuilder _html = new StringBuilder();

        public override bool HasNativeHyperlinkSupport { get { return true; } }

        public string SessionId { get; set; }

        public string Html { get { return _html.ToString(); } }

        private string NewlineToBR(string value)
        {
            return value.Replace(Environment.NewLine, "<br/>");
        }

        public void EchoCommand(string value)
        {
            _html.AppendFormat("<font color='green'>{0}</font>", NewlineToBR(value));
        }

        public override void WriteInfo(string value)
        {
            _html.AppendFormat("<i>{0}</i>", NewlineToBR(value));
        }

        public override void WriteCommandOutput(string value)
        {
            _html.Append(NewlineToBR(value));
        }

        public override void WriteError(string value)
        {
            _html.AppendFormat("<b><font color='red'>{0}</font></b>", NewlineToBR(value));
        }

        public override void WriteWarning(string value)
        {
            _html.AppendFormat("<font color='red'>{0}</font>", NewlineToBR(value));
        }

        public override void WriteLink(string text, string command)
        {
            if (String.IsNullOrEmpty(text))
            {
                text = command; // TODO
            }
            _html.AppendFormat(
                "<a href='/Analysis/ExecuteCommand?sessionId={0}&command={1}'>{2}</a>",
                SessionId, command, text);
        }

        public override void ClearScreen()
        {
            _html.Clear();
        }
    }
}