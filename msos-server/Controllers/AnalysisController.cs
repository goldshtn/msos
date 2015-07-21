using msos;
using msos_server.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace msos_server.Controllers
{
    public class AnalysisController : BaseController
    {
        public ActionResult Attach(int processId)
        {
            // TODO
            return View("Error", new ErrorModel("Attaching to a process is not yet implemented"));
        }

        public ActionResult OpenDump(string dumpFile)
        {
            HtmlPrinter printer = new HtmlPrinter();
            CommandExecutionContext context = new CommandExecutionContext()
            {
                Printer = printer
            };
            AnalysisTarget target = new AnalysisTarget(dumpFile, context);
            AnalysisSession session = new AnalysisSession(context, target, printer);
            AddToSession(session);
            return View("Index", new TargetModel(printer, session.Id));
        }

        public ActionResult ExecuteCommand(string sessionId, string command)
        {
            var session = SessionForSessionId(sessionId);
            if (session == null)
            {
                return View("Error", new ErrorModel("Couldn't find a session with the id " + sessionId));
            }
            session.Printer.EchoCommand("> " + command + Environment.NewLine);
            session.Context.ExecuteCommand(command);
            return View("Index", new TargetModel(session.Printer, session.Id));
        }

        public ActionResult ReconnectToSession(string sessionId)
        {
            var session = SessionForSessionId(sessionId);
            if (session == null)
            {
                return View("Error", new ErrorModel("Couldn't find a session with the id " + sessionId));
            }
            return View("Index", new TargetModel(session.Printer, session.Id));
        }

        public ActionResult CloseSession(string sessionId)
        {
            RemoveSession(sessionId);
            return Redirect("/Home/Index");
        }
    }
}