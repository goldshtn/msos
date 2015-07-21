using msos_server.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace msos_server.Controllers
{
    public class BaseController : Controller
    {
        internal void AddToSession(AnalysisSession session)
        {
            Session.Add("session-" + session.Id, session);
        }

        internal AnalysisSession SessionForSessionId(string sessionId)
        {
            return (AnalysisSession)Session["session-" + sessionId];
        }

        internal IEnumerable<string> GetOpenSessionIds()
        {
            return from key in Session.Keys.Cast<string>()
                   where key.StartsWith("session-")
                   select key.Substring("session-".Length);
        }

        internal void RemoveSession(string sessionId)
        {
            Session.Remove(sessionId);
        }
    }
}